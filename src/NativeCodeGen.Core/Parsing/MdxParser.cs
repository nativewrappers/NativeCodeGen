using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Parsing;

public class MdxParser
{
    private static readonly HashSet<string> AllowedSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Parameters",
        "Return value",
        "Examples"
    };

    private readonly FrontmatterParser _frontmatterParser = new();
    private readonly MdxComponentParser _componentParser = new();

    public ParseResult<NativeDefinition> Parse(string content, string filePath)
    {
        var result = new ParseResult<NativeDefinition>();

        // Parse frontmatter
        var (frontmatter, frontmatterEndLine, frontmatterError) = _frontmatterParser.Parse(content, filePath);
        if (frontmatterError != null)
        {
            result.Errors.Add(frontmatterError);
            return result;
        }

        // Parse markdown after frontmatter
        var markdownContent = string.Join('\n', content.Split('\n').Skip(frontmatterEndLine));
        var pipeline = new MarkdownPipelineBuilder().Build();
        var document = Markdown.Parse(markdownContent, pipeline);

        var native = new NativeDefinition
        {
            Namespace = frontmatter!.Ns,
            Aliases = frontmatter.Aliases,
            ApiSet = frontmatter.Apiset,
            SourceFile = filePath
        };

        // Track what we've parsed
        bool foundNativeName = false;
        bool foundCodeBlock = false;
        bool foundParameters = false;
        bool foundReturnValue = false;
        string? currentSection = null;
        var descriptionParts = new List<string>();
        var parameterDescriptions = new Dictionary<string, string>();

        foreach (var block in document)
        {
            switch (block)
            {
                case HeadingBlock heading when heading.Level == 2:
                    var headingText = GetHeadingText(heading);

                    if (!foundNativeName)
                    {
                        // First ## heading is the native name
                        native.Name = headingText;
                        foundNativeName = true;
                        currentSection = null;
                    }
                    else if (AllowedSections.Contains(headingText))
                    {
                        currentSection = headingText;
                        if (headingText.Equals("Parameters", StringComparison.OrdinalIgnoreCase))
                            foundParameters = true;
                        else if (headingText.Equals("Return value", StringComparison.OrdinalIgnoreCase))
                            foundReturnValue = true;
                    }
                    else if (!headingText.Equals(native.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        // Unknown section
                        result.Errors.Add(new ParseError
                        {
                            FilePath = filePath,
                            Line = frontmatterEndLine + heading.Line + 1,
                            Column = 1,
                            Message = $"Unknown section '## {headingText}'. Allowed sections: Parameters, Return value, Examples"
                        });
                    }
                    break;

                case FencedCodeBlock codeBlock when !foundCodeBlock && foundNativeName:
                    var codeLines = codeBlock.Lines.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (codeLines.Length >= 2)
                    {
                        // Parse hash from first line
                        var hashLine = codeLines[0].Trim();
                        if (hashLine.StartsWith("//"))
                        {
                            var hashPart = hashLine[2..].Trim();
                            if (hashPart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            {
                                native.Hash = hashPart;
                            }
                            else
                            {
                                result.Errors.Add(new ParseError
                                {
                                    FilePath = filePath,
                                    Line = frontmatterEndLine + codeBlock.Line + 1,
                                    Column = 1,
                                    Message = $"Invalid hash format. Expected '// 0xHEX', got '{hashLine}'"
                                });
                            }
                        }
                        else
                        {
                            result.Errors.Add(new ParseError
                            {
                                FilePath = filePath,
                                Line = frontmatterEndLine + codeBlock.Line + 1,
                                Column = 1,
                                Message = $"First line of code block must be hash comment (// 0xHEX)"
                            });
                        }

                        // Parse signature from second line
                        var signatureLine = codeLines[1].Trim();
                        try
                        {
                            var lexer = new SignatureLexer(signatureLine);
                            var tokens = lexer.Tokenize();
                            var parser = new SignatureParser(tokens, filePath, frontmatterEndLine + codeBlock.Line + 2);
                            var (returnType, name, parameters) = parser.ParseSignature();

                            native.ReturnType = returnType;
                            native.Parameters = parameters;

                            // Verify name matches heading
                            if (!name.Equals(native.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                result.Warnings.Add(new ParseWarning
                                {
                                    FilePath = filePath,
                                    Line = frontmatterEndLine + codeBlock.Line + 2,
                                    Column = 1,
                                    Message = $"Function name '{name}' doesn't match heading '{native.Name}'"
                                });
                            }
                        }
                        catch (ParseException ex)
                        {
                            result.Errors.Add(new ParseError
                            {
                                FilePath = filePath,
                                Line = ex.Line,
                                Column = ex.Column,
                                Message = ex.Message
                            });
                        }
                    }
                    else
                    {
                        result.Errors.Add(new ParseError
                        {
                            FilePath = filePath,
                            Line = frontmatterEndLine + codeBlock.Line + 1,
                            Column = 1,
                            Message = "Code block must contain hash comment and signature"
                        });
                    }
                    foundCodeBlock = true;
                    break;

                case ParagraphBlock paragraph when currentSection == null && foundCodeBlock:
                    // Description text (before any section)
                    var paragraphText = GetParagraphText(paragraph);
                    descriptionParts.Add(paragraphText);

                    // Check for embedded enum references
                    var enumRefs = _componentParser.ParseEmbeddedEnums(paragraphText);
                    foreach (var enumRef in enumRefs)
                    {
                        if (!native.UsedEnums.Contains(enumRef.Name))
                            native.UsedEnums.Add(enumRef.Name);
                    }

                    // Check for shared example references
                    var exampleRefs = _componentParser.ParseSharedExamples(paragraphText);
                    foreach (var exampleRef in exampleRefs)
                    {
                        if (!native.RelatedExamples.Contains(exampleRef.Name))
                            native.RelatedExamples.Add(exampleRef.Name);
                    }
                    break;

                case ListBlock listBlock when currentSection?.Equals("Parameters", StringComparison.OrdinalIgnoreCase) == true:
                    ParseParameterList(listBlock, native, parameterDescriptions, out var documentedParamOrder, filePath, frontmatterEndLine);

                    // Validate parameter count, names, and order
                    ValidateParameters(native.Parameters, documentedParamOrder, result, filePath, frontmatterEndLine + listBlock.Line);
                    break;

                case ParagraphBlock returnParagraph when currentSection?.Equals("Return value", StringComparison.OrdinalIgnoreCase) == true:
                    native.ReturnDescription = GetParagraphText(returnParagraph);
                    break;
            }
        }

        // Join description parts and clean up embedded references
        if (descriptionParts.Count > 0)
        {
            native.Description = _componentParser.NormalizeDescription(string.Join("\n\n", descriptionParts));
        }

        // Clean return description
        if (!string.IsNullOrEmpty(native.ReturnDescription))
        {
            native.ReturnDescription = _componentParser.NormalizeDescription(native.ReturnDescription);
        }

        // Also scan the entire markdown content for SharedExamples (may be in Examples section)
        var allExampleRefs = _componentParser.ParseSharedExamples(markdownContent);
        foreach (var exampleRef in allExampleRefs)
        {
            if (!native.RelatedExamples.Contains(exampleRef.Name))
                native.RelatedExamples.Add(exampleRef.Name);
        }

        // Scan for callouts (notes, warnings, etc.)
        native.Callouts = _componentParser.ParseCallouts(markdownContent);

        // Apply parameter descriptions (cleaned)
        foreach (var param in native.Parameters)
        {
            if (parameterDescriptions.TryGetValue(param.Name, out var desc))
            {
                param.Description = _componentParser.NormalizeDescription(desc);
            }
        }

        // Validation
        if (!foundNativeName)
        {
            result.Errors.Add(new ParseError
            {
                FilePath = filePath,
                Line = frontmatterEndLine + 1,
                Column = 1,
                Message = "Missing native name heading (## NATIVE_NAME)"
            });
        }

        if (!foundCodeBlock)
        {
            result.Errors.Add(new ParseError
            {
                FilePath = filePath,
                Line = frontmatterEndLine + 1,
                Column = 1,
                Message = "Missing code block with hash and signature"
            });
        }

        // Check if Parameters section is missing when there are params
        if (native.Parameters.Count > 0 && !foundParameters)
        {
            result.Errors.Add(new ParseError
            {
                FilePath = filePath,
                Line = frontmatterEndLine + 1,
                Column = 1,
                Message = "Missing ## Parameters section (required when signature has parameters)"
            });
        }

        // Warning for missing Return value section when non-void
        if (native.ReturnType.Category != TypeCategory.Void && !foundReturnValue)
        {
            result.Warnings.Add(new ParseWarning
            {
                FilePath = filePath,
                Line = frontmatterEndLine + 1,
                Column = 1,
                Message = "Missing ## Return value section (recommended for non-void return type)"
            });
        }

        // Validate required params don't follow optional params
        bool hasOptional = false;
        for (int i = 0; i < native.Parameters.Count; i++)
        {
            var param = native.Parameters[i];
            if (param.HasDefaultValue)
            {
                hasOptional = true;
            }
            else if (hasOptional)
            {
                result.Errors.Add(new ParseError
                {
                    FilePath = filePath,
                    Line = frontmatterEndLine + 1,
                    Column = 1,
                    Message = $"Required parameter '{param.Name}' follows optional parameter"
                });
            }
        }

        result.Value = native;
        return result;
    }

    private void ParseParameterList(ListBlock listBlock, NativeDefinition native,
        Dictionary<string, string> parameterDescriptions, out List<string> documentedParamOrder,
        string filePath, int baseLineNumber)
    {
        documentedParamOrder = new List<string>();

        foreach (var item in listBlock)
        {
            if (item is not ListItemBlock listItem)
                continue;

            var itemText = GetListItemText(listItem);

            // Extract embedded enum references from parameter descriptions
            var enumRefs = _componentParser.ParseEmbeddedEnums(itemText);
            foreach (var enumRef in enumRefs)
            {
                if (!native.UsedEnums.Contains(enumRef.Name))
                    native.UsedEnums.Add(enumRef.Name);
            }

            // Parse parameter name and description
            // Format: * **paramName**: Description text
            if (itemText.StartsWith("**"))
            {
                var endBold = itemText.IndexOf("**", 2);
                if (endBold > 2)
                {
                    var paramName = itemText[2..endBold];
                    var description = endBold + 2 < itemText.Length
                        ? itemText[(endBold + 2)..].TrimStart(':', ' ')
                        : string.Empty;

                    documentedParamOrder.Add(paramName);

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        parameterDescriptions[paramName] = description;
                    }
                }
            }
        }
    }

    private static void ValidateParameters(List<NativeParameter> signatureParams, List<string> documentedParams,
        ParseResult<NativeDefinition> result, string filePath, int lineNumber)
    {
        // Check parameter count
        if (signatureParams.Count != documentedParams.Count)
        {
            result.Errors.Add(new ParseError
            {
                FilePath = filePath,
                Line = lineNumber,
                Column = 1,
                Message = $"Parameter count mismatch: signature has {signatureParams.Count} parameter(s), but ## Parameters section documents {documentedParams.Count}"
            });
            return; // Don't continue with name/order validation if counts don't match
        }

        // Check parameter names and order
        for (int i = 0; i < signatureParams.Count; i++)
        {
            var signatureParamName = signatureParams[i].Name;
            var documentedParamName = documentedParams[i];

            if (!signatureParamName.Equals(documentedParamName, StringComparison.Ordinal))
            {
                // Check if it's a name mismatch or order mismatch
                var signatureIndex = documentedParams.IndexOf(signatureParamName);
                var documentedIndex = signatureParams.FindIndex(p => p.Name == documentedParamName);

                if (signatureIndex >= 0 && documentedIndex >= 0)
                {
                    // Both names exist but in wrong order
                    result.Errors.Add(new ParseError
                    {
                        FilePath = filePath,
                        Line = lineNumber,
                        Column = 1,
                        Message = $"Parameter order mismatch at position {i + 1}: signature has '{signatureParamName}', but ## Parameters section has '{documentedParamName}'"
                    });
                }
                else
                {
                    // Name doesn't match
                    result.Errors.Add(new ParseError
                    {
                        FilePath = filePath,
                        Line = lineNumber,
                        Column = 1,
                        Message = $"Parameter name mismatch at position {i + 1}: signature has '{signatureParamName}', but ## Parameters section has '{documentedParamName}'"
                    });
                }
            }
        }
    }

    private static string GetHeadingText(HeadingBlock heading)
    {
        if (heading.Inline == null)
            return string.Empty;

        return string.Join("", heading.Inline.Select(GetInlineText));
    }

    private static string GetParagraphText(ParagraphBlock paragraph)
    {
        if (paragraph.Inline == null)
            return string.Empty;

        return string.Join("", paragraph.Inline.Select(GetInlineText));
    }

    private static string GetListItemText(ListItemBlock listItem)
    {
        var parts = new List<string>();
        foreach (var block in listItem)
        {
            if (block is ParagraphBlock para)
            {
                parts.Add(GetParagraphText(para));
            }
        }
        return string.Join(" ", parts);
    }

    private static string GetInlineText(Inline inline) => inline switch
    {
        LiteralInline literal => literal.Content.ToString(),
        EmphasisInline emphasis when emphasis.DelimiterCount == 2 => $"**{string.Join("", emphasis.Select(GetInlineText))}**",
        EmphasisInline emphasis => $"*{string.Join("", emphasis.Select(GetInlineText))}*",
        CodeInline code => $"`{code.Content}`",
        LinkInline link => string.Join("", link.Select(GetInlineText)),
        HtmlInline html => html.Tag,
        LineBreakInline => "\n",
        _ => inline.ToString() ?? string.Empty
    };
}

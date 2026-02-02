# {{PACKAGE_NAME}}

Auto-generated native function bindings with class-based API.

## Installation

```bash
npm install {{PACKAGE_NAME}}
```

## Usage

```typescript
import { Entity, Ped, Vehicle, World } from '{{PACKAGE_NAME}}';

// Use handle classes
const ped = new Ped(playerPedId);
const coords = ped.Coords;
const vehicle = ped.CurrentVehicle;

// Method chaining with setters
ped.setHealth(100).setArmor(50).setVisible(true);

// Use namespace utilities
const weather = World.getWeatherTypeTransition();
```

## API

### Handle Classes

Classes that wrap game entity handles:

- `Entity` - Base class for all game entities
- `Ped` - Pedestrian/character entities
- `Vehicle` - Vehicle entities
- `Prop` - Prop/object entities
- `Player` - Player-specific functions
- `Pickup` - Pickup entities
- `Cam` - Camera control
- `Interior` - Interior management
- `AnimScene` - Animation scenes
- `ItemSet` - Item set management
- `PersChar` - Persistent characters
- `PropSet` - Prop sets
- `Volume` - Volume management

### Task Classes

Wrap entity references for task-related natives:

- `PedTask` - Ped task operations
- `VehicleTask` - Vehicle task operations

### Model Classes

Wrap hash values for model/streaming natives:

- `BaseModel` - Base model class
- `PedModel` - Ped model operations
- `VehicleModel` - Vehicle model operations
- `WeaponModel` - Weapon model operations

### Namespace Utilities

Static classes for natives that don't operate on handles:

- `World` - Weather, time, world state
- `Streaming` - Asset loading/unloading
- `Graphics` - Drawing, particles, effects
- `Audio` - Sound playback
- `Hud` - UI elements
- `Network` - Multiplayer functions
- And more...

## Features

### Getters/Setters

Methods are automatically converted to TypeScript property accessors:

```typescript
// Instead of: ped.getHealth()
const health = ped.Health;

// Instead of: ped.setHealth(100)
ped.Health = 100;
```

### Method Chaining

Setter methods return `this` for fluent API:

```typescript
ped.setHealth(100)
   .setArmor(50)
   .setVisible(true)
   .setInvincible(false);
```

### Type Safety

Full TypeScript types with documented parameters:

```typescript
// Enums for type-safe parameters
ped.giveWeapon(eWeaponHash.WEAPON_REVOLVER_CATTLEMAN, 100, true);

// Nullable returns handled
const vehicle = ped.CurrentVehicle; // Vehicle | null
```

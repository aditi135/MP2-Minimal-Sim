# MP2-Minimal-Sim

A VR factory simulation built with Unity and the XR Interaction Toolkit. Generators produce items on a timer, a store collects and serves them at delivery sockets, and the player grabs items and places them onto display boards to reveal letters.

---

## Part 1 — How to Play

### Game Flow

1. **Simulation starts automatically.** The `GameManager` boots into the *Running* state on scene load (configurable via `startOnAwake`). A simulation clock begins ticking.

2. **Generators produce items.** Each `AutoGenerator` in the scene creates items at a fixed interval (e.g. every 60 seconds). Produced items are handed to the `Store`.

3. **Items appear at the store.** The `Store` places each item at the next open delivery point (socket). If all delivery sockets for that item type are full, new items are rejected until a socket is freed.

4. **Pick up items.** Walk up to a store delivery point and grab an item with your VR controller. The item detaches from the socket and the slot becomes available again.

5. **Boost generators (optional).** Some generators have boost slots (small XRSocket cubes near the machine). Place boost cubes into these slots to speed up production. Each filled slot multiplies the production speed by the configured `speedBoostPerSlot` factor.

6. **Place items on display boards.** Carry items to a `DisplayBoard` and socket them in. Each board only accepts items with a specific tag (e.g. "Red", "Green"). When every slot on a board is filled, the board reveals a letter on its screen.

7. **View generator info.** Press the configured toggle button to bring up the `GeneratorInfoBoard`, a floating HUD that shows the current effective production interval for every generator.

8. **Win condition.** Fill all display boards to reveal all the letters.

### Controls

| Action | Description |
|---|---|
| Grab trigger | Pick up / release items |
| Toggle button (configurable) | Show / hide generator info board |

---

## Part 2 — Developer Guide: Extending the Game

The project follows a modular, event-driven architecture. Each component subscribes to the `GameManager`'s `OnStateChanged` event and reacts to the four game states: **Initializing**, **Running**, **Paused**, and **GameOver**.

### Architecture Overview

```
GameManager (singleton)
  │  broadcasts OnStateChanged(GameState)
  │
  ├── AutoGenerator
  │     produces items on a timer
  │     fires OnItemProduced(GameObject)
  │
  ├── Store
  │     subscribes to AutoGenerator.OnItemProduced
  │     places items at delivery sockets
  │     fires OnItemReceived / OnItemPickedUp
  │
  ├── DisplayBoard
  │     accepts items via XRSocketInteractors
  │     filters by tag, reveals a letter when full
  │
  └── GeneratorInfoBoard
        reads AutoGenerator stats and displays them
```

### How to Add a New Item Type

1. **Create the prefab.** Build a new GameObject prefab (e.g. a sphere or custom mesh). It must have:
   - A `Rigidbody` component
   - An `XRGrabInteractable` component
   - (The `Item` script will be auto-added by the generator if missing.)

2. **Assign a Unity tag.** In *Edit > Project Settings > Tags and Layers*, create a new tag (e.g. `"Blue"`). Set the prefab's tag to this value.

3. **Done.** The prefab is now ready to be referenced by generators and accepted by stores / display boards.

### How to Add a New Generator

1. **Create the generator GameObject** in the scene (or duplicate an existing one).

2. **Add the `AutoGenerator` component** and configure the Inspector fields:

   | Field | Purpose |
   |---|---|
   | `itemPrefab` | The item prefab this generator will produce |
   | `spawnPoint` | A child Transform where items spawn (defaults to the generator itself) |
   | `baseProductionInterval` | Seconds between each produced item |
   | `itemName` / `itemValue` | Metadata written to each `Item` component |
   | `slots` | Array of `XRSocketInteractor` children that act as boost slots |
   | `requireSlotToStart` | If true, production only starts when at least one boost slot is filled |
   | `speedBoostPerSlot` | Fraction by which each additional boost slot accelerates production |

3. **Wire it to a Store.** On the Store's `ItemSlot` list, add a new entry:
   - Set `itemTag` to the Unity tag of the produced item.
   - Drag the new `AutoGenerator` into the `generator` field.
   - Create one or more child Transforms as delivery points and assign them to `deliveryPoints`.

4. The generator will auto-subscribe to `GameManager.OnStateChanged` and begin producing when the simulation enters the *Running* state.

### How to Add a New Display Board

1. **Create the board GameObject** in the scene.

2. **Add `XRSocketInteractor` children** — one per item the board should accept. These are the slots the player will place items into.

3. **Add the `DisplayBoard` component** and configure:

   | Field | Purpose |
   |---|---|
   | `slots` | The `XRSocketInteractor` children you just created |
   | `allowedItemTag` | The Unity tag this board accepts (e.g. `"Red"`). Leave empty to accept any item. |
   | `displayText` | A `TMP_Text` component on the board where the letter will appear |
   | `letter` | The string to display when all slots are filled |

4. The board implements `IXRSelectFilter` and automatically registers itself on each socket, so only items with the matching tag can be socketed.

### How to Add a Generator Info Board

1. **Create an empty GameObject** and add the `GeneratorInfoBoard` component.

2. **Configure:**

   | Field | Purpose |
   |---|---|
   | `toggleAction` | An `InputActionReference` (e.g. a button press) that toggles the board on/off |
   | `generators` | Array of `AutoGenerator` references to display stats for |
   | `distanceFromPlayer` | How far in front of the camera the floating board appears |
   | `boardSize` / `fontSize` | Visual dimensions of the auto-generated world-space canvas |

3. The board creates its own Canvas + TMP_Text at runtime — no manual UI setup needed.

### How to Add a New Store

1. **Create a GameObject** and add the `Store` component.

2. **For each item type the store should handle**, add an `ItemSlot` entry:
   - `itemTag` — must match the Unity tag on the items.
   - `generator` — the `AutoGenerator` that produces items for this slot.
   - `deliveryPoints` — child Transforms where produced items will be placed (array length = capacity).

3. The Store auto-subscribes to each generator's `OnItemProduced` event, tags the item, and parks it at the first open delivery point. When the player grabs an item (changing its parent), the Store detects the pickup and frees the socket.

### Key Events You Can Subscribe To

| Component | Event | Payload | Fires when |
|---|---|---|---|
| `GameManager` | `OnStateChanged` | `GameState` | State transitions |
| `AutoGenerator` | `OnItemProduced` | `GameObject` | A new item is instantiated |
| `Store` | `OnItemReceived` | `GameObject` | An item is placed at a delivery socket |
| `Store` | `OnItemPickedUp` | `GameObject` | A player picks up an item |
| `DisplayBoard` | `OnBoardCompleted` | — | All slots filled |
| `DisplayBoard` | `OnBoardIncomplete` | — | A slot emptied after being complete |
| `Item` | `OnStateChanged` | `Item, ItemState` | Item state transition (Created → InStore → Held → Released) |

### Project Structure

```
Assets/Scripts/
├── Core/
│   └── GameManager.cs          Singleton simulation controller
├── Items/
│   └── Item.cs                 Grabbable item with state machine
├── Production/
│   ├── AutoGenerator.cs        Timer-based item producer with boost slots
│   └── Store.cs                Multi-slot item storage + delivery
├── Display/
│   ├── DisplayBoard.cs         Socket-based letter reveal board
│   └── GeneratorInfoBoard.cs   Floating HUD for generator stats
└── Tests/
    └── TestCube.cs             Debug position logger
```

# Game Data Authoring

Use the ScriptableObject definitions in `Adventure.GameData.Definitions` together with the editor tooling to add new classes and abilities.

## Creating abilities
1. In the Project window, right-click and choose **Create > Adventure > Definitions > Ability Definition**.
2. Fill out the following fields:
   - **Id**: Unique string identifier (used for registry lookups).
   - **Display Name / Description**: Player-facing text.
   - **Icon**: Optional sprite for UI.
   - **Cooldown Seconds** and **Resource Costs**: Tuning values for how the ability is used.
   - **Tags**: Keywords that power search and filtering in the Definition Manager window.
   - **Effect**: Configure either the growth **Curve** or enable **Use Effect Formula** with a formula such as `{base} + ({level} * 2)`.
3. Save the asset. The registry builder will pick it up automatically.

## Creating classes
1. In the Project window, choose **Create > Adventure > Definitions > Class Definition**.
2. Assign a unique **Id**, **Display Name**, and optional **Icon/Description**.
3. Reference a **Stat Block** asset in **Base Stats**. You can reuse existing blocks or create one via **Create > Adventure > Definitions > Stat Block**.
4. Add **Abilities** by dragging Ability Definition assets into the list.
5. Use **Tags** to organize your class for filtering in tooling.

## Updating the registry
The registry is rebuilt automatically when assets change, but you can also trigger it manually:
- Open **Adventure > Definition Manager** and click **Refresh**.
- Or run **Adventure.Editor.Registry.RegistryBuilder.BuildRegistry()** from an editor script or the Immediate window.

The generated registry asset lives at `Assets/Resources/Registry/IDRegistry.asset` so it is available at runtime via `DefinitionDB`.

## Using the Definition Manager window
- Open it from **Adventure > Definition Manager**.
- Use the search box and tag filter to locate assets quickly.
- Bulk tools help duplicate entries (with a suffix), assign default icons, and strip null references from class ability lists.

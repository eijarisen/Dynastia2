namespace BearAdventure.Domain.World;

public static class GeneratedStructureLayoutService
{
    public static IReadOnlyList<GeneratedStructureLayout> BuildAll(IslandDefinition island)
    {
        ArgumentNullException.ThrowIfNull(island);
        return island.GeneratedStructures
            .OrderBy(s => s.StructureId)
            .Select(s => Build(island, s))
            .ToArray();
    }

    public static GeneratedStructureLayout Build(
        IslandDefinition island,
        GeneratedStructureDefinition structure)
    {
        ArgumentNullException.ThrowIfNull(island);
        ArgumentNullException.ThrowIfNull(structure);

        int width = Math.Clamp(structure.WidthCells, 4, island.WidthCells);
        int left = Math.Clamp(
            structure.CenterCellX - width / 2,
            0,
            island.WidthCells - width);
        int right = left + width - 1;
        int baseLevel = structure.BaseSurfaceLevel;

        // Entrances face toward the middle of the island, which keeps the access
        // route away from the reserved shorelines in normal generation.
        bool doorOnLeft = structure.CenterCellX >= island.WidthCells / 2;
        int door = doorOnLeft ? left : right;

        var solid = new HashSet<UndergroundCell>();
        var background = new HashSet<UndergroundCell>();
        var ladder = new HashSet<UndergroundCell>();
        var carved = new HashSet<UndergroundCell>();
        var access = new HashSet<UndergroundCell>();

        void AddFoundation()
        {
            for (int x = left; x <= right; x++)
                solid.Add(new UndergroundCell(x, baseLevel));
        }

        void AddAccess()
        {
            int direction = doorOnLeft ? -1 : 1;
            for (int step = 0; step <= 2; step++)
            {
                int x = door + direction * step;
                if (x < 0 || x >= island.WidthCells) continue;

                // Keep a two-cell-high doorway/path clear. A generated foundation
                // under the route prevents an unlucky small depression from
                // making the entrance unreachable.
                carved.Add(new UndergroundCell(x, baseLevel + 1));
                carved.Add(new UndergroundCell(x, baseLevel + 2));
                access.Add(new UndergroundCell(x, baseLevel + 1));
                access.Add(new UndergroundCell(x, baseLevel + 2));
                solid.Add(new UndergroundCell(x, baseLevel));
            }
        }

        AddFoundation();
        AddAccess();

        if (structure.Kind == GeneratedStructureKind.House)
        {
            const int wallTop = 3;
            for (int level = 1; level <= wallTop; level++)
            {
                var leftCell = new UndergroundCell(left, baseLevel + level);
                var rightCell = new UndergroundCell(right, baseLevel + level);

                if (!(doorOnLeft && level <= 2)) solid.Add(leftCell);
                else carved.Add(leftCell);

                if (!(!doorOnLeft && level <= 2)) solid.Add(rightCell);
                else carved.Add(rightCell);
            }

            for (int x = left; x <= right; x++)
                solid.Add(new UndergroundCell(x, baseLevel + 4));

            for (int x = left + 1; x <= right - 1; x++)
                for (int level = 1; level <= 3; level++)
                {
                    var cell = new UndergroundCell(x, baseLevel + level);
                    background.Add(cell);
                    carved.Add(cell);
                }
        }
        else
        {
            const int wallTop = 5;
            for (int level = 1; level <= wallTop; level++)
            {
                var leftCell = new UndergroundCell(left, baseLevel + level);
                var rightCell = new UndergroundCell(right, baseLevel + level);

                if (!(doorOnLeft && level <= 2)) solid.Add(leftCell);
                else carved.Add(leftCell);

                if (!(!doorOnLeft && level <= 2)) solid.Add(rightCell);
                else carved.Add(rightCell);
            }

            int ladderX = doorOnLeft
                ? Math.Clamp(right - 2, left + 1, right - 1)
                : Math.Clamp(left + 2, left + 1, right - 1);

            if (ladderX == structure.Chest.CellX)
                ladderX = ladderX < structure.CenterCellX
                    ? Math.Min(right - 1, ladderX + 1)
                    : Math.Max(left + 1, ladderX - 1);

            for (int level = 1; level <= 5; level++)
            {
                var cell = new UndergroundCell(ladderX, baseLevel + level);
                ladder.Add(cell);
                carved.Add(cell);
            }

            // A second traversable floor makes the castle a real two-storey space.
            // Leave the ladder cell open so the bear and worker navigation can pass.
            for (int x = left + 1; x <= right - 1; x++)
                if (x != ladderX)
                    solid.Add(new UndergroundCell(x, baseLevel + 3));

            for (int x = left; x <= right; x++)
                solid.Add(new UndergroundCell(x, baseLevel + 6));

            for (int x = left + 1; x <= right - 1; x++)
                for (int level = 1; level <= 5; level++)
                {
                    var cell = new UndergroundCell(x, baseLevel + level);
                    if (solid.Contains(cell)) continue;
                    background.Add(cell);
                    carved.Add(cell);
                }
        }

        int minX = Math.Min(left, access.Count == 0 ? left : access.Min(c => c.CellX));
        int maxX = Math.Max(right, access.Count == 0 ? right : access.Max(c => c.CellX));
        int maxLevel = structure.Kind == GeneratedStructureKind.Castle
            ? baseLevel + 6
            : baseLevel + 4;

        return new GeneratedStructureLayout
        {
            StructureId = structure.StructureId,
            Kind = structure.Kind,
            LeftCellX = left,
            RightCellX = right,
            BaseLogicalLevel = baseLevel,
            DoorCellX = door,
            SolidCells = solid,
            BackgroundCells = background,
            LadderCells = ladder,
            CarvedCells = carved,
            AccessCells = access,
            ReservedBounds = new WorldRect(
                WorldGrid.LandLeft + minX * WorldGrid.CellSize,
                -maxLevel * WorldGrid.CellSize,
                (maxX - minX + 1) * WorldGrid.CellSize,
                (maxLevel - baseLevel + 1) * WorldGrid.CellSize),
        };
    }
}

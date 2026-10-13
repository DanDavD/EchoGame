using System;
using System.Linq;
using EchoProtocol.Level;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Tests
{
    public class DungeonGridTests
    {
        // Smallest valid map: sealed border, one of everything, three relic shrines.
        private const string ValidMap = @"
; comment lines and blank lines are ignored

#########
#S..R..E#
#.#####.#
#R..A..R#
#.#####M#
#########";

        [Test]
        public void Parse_ReadsDimensionsAndCells()
        {
            DungeonGrid grid = DungeonGrid.Parse(ValidMap);

            Assert.AreEqual(9, grid.Width);
            Assert.AreEqual(6, grid.Height);
            Assert.AreEqual(DungeonGrid.Wall, grid[0, 0]);
            Assert.AreEqual(DungeonGrid.Start, grid[1, 1]);
            Assert.AreEqual(DungeonGrid.Altar, grid[4, 3]);
        }

        [Test]
        public void Parse_RaggedRows_Throws()
        {
            Assert.Throws<FormatException>(() => DungeonGrid.Parse("###\n##\n###"));
        }

        [Test]
        public void Parse_UnknownCharacter_Throws()
        {
            Assert.Throws<FormatException>(() => DungeonGrid.Parse("###\n#?#\n###"));
        }

        [Test]
        public void Parse_EmptyText_Throws()
        {
            Assert.Throws<FormatException>(() => DungeonGrid.Parse("; nothing here\n\n"));
        }

        [Test]
        public void Validate_CompleteMap_HasNoErrors()
        {
            Assert.IsEmpty(DungeonGrid.Parse(ValidMap).Validate());
        }

        [Test]
        public void Validate_MissingAltar_IsReported()
        {
            DungeonGrid grid = DungeonGrid.Parse(ValidMap.Replace('A', '.'));
            Assert.IsTrue(grid.Validate().Any(e => e.Contains("altar")));
        }

        [Test]
        public void Validate_WrongRelicCount_IsReported()
        {
            DungeonGrid grid = DungeonGrid.Parse(ValidMap.Replace("R..E", "...E"));
            Assert.IsTrue(grid.Validate().Any(e => e.Contains("relic")));
        }

        [Test]
        public void Validate_OpenBorder_IsReported()
        {
            DungeonGrid grid = DungeonGrid.Parse(ValidMap.Replace("#S..R..E#", ".S..R..E#"));
            Assert.IsTrue(grid.Validate().Any(e => e.Contains("border")));
        }

        [Test]
        public void Validate_SealedOffObjective_IsReported()
        {
            // The monster sits in a pocket (columns 5-7 of the last row) walled off from the rest of the map.
            const string sealedMap = @"
#########
#S.R.R.R#
#.#######
#.A.#.M.#
#########";
            DungeonGrid grid = DungeonGrid.Parse(sealedMap);
            Assert.IsTrue(grid.Validate().Any(e => e.Contains("'M'") && e.Contains("cannot be reached")));
        }

        [Test]
        public void Find_ReturnsEveryMatchingCell()
        {
            DungeonGrid grid = DungeonGrid.Parse(ValidMap);
            Assert.AreEqual(3, grid.Find(DungeonGrid.Relic).Count);
            Assert.AreEqual(1, grid.Find(DungeonGrid.Start).Count);
        }

        [Test]
        public void CellToWorld_CentresMapOnOriginWithNorthUp()
        {
            DungeonGrid grid = DungeonGrid.Parse(ValidMap);

            // Centre column / row of the 9x6 map is (4, 2.5).
            Vector3 northWest = grid.CellToWorld(0, 0, 5f);
            Vector3 southEast = grid.CellToWorld(8, 5, 5f);

            Assert.AreEqual(-20f, northWest.x, 1e-4f);
            Assert.AreEqual(12.5f, northWest.z, 1e-4f);
            Assert.AreEqual(20f, southEast.x, 1e-4f);
            Assert.AreEqual(-12.5f, southEast.z, 1e-4f);
        }

        [Test]
        public void WallRuns_MergeAdjacentWalls()
        {
            DungeonGrid grid = DungeonGrid.Parse(ValidMap);

            // Row 2 is "#.#####.#": wall, floor, five walls, floor, wall.
            var runs = grid.WallRuns(2);

            Assert.AreEqual(3, runs.Count);
            Assert.AreEqual(new Vector2Int(0, 1), runs[0]);
            Assert.AreEqual(new Vector2Int(2, 5), runs[1]);
            Assert.AreEqual(new Vector2Int(8, 1), runs[2]);
        }

        [Test]
        public void ShippedCatacombsLayout_IsValid()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Level/Catacombs.txt");
            Assert.IsNotNull(asset, "Assets/Level/Catacombs.txt is missing");

            DungeonGrid grid = DungeonGrid.Parse(asset.text);

            Assert.IsEmpty(grid.Validate(), "The shipped level must always be playable.");
        }
    }
}

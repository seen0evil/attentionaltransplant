using System.Collections.Generic;
using AttentionalTransplants.DonorDataCollection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttentionalTransplants.DonorDataCollectionTests
{
    public class AttentionGridZoneGeneratorTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void GenerateZonesCreatesConfiguredGridWithUniqueIds()
        {
            AttentionGridZoneGenerator generator = CreateGenerator(5, 5);
            generator.GenerateZones();

            AttentionZone[] zones = generator.GetComponentsInChildren<AttentionZone>();
            HashSet<string> zoneIds = new();

            foreach (AttentionZone zone in zones)
            {
                zoneIds.Add(zone.ResolvedZoneId);
                Assert.IsTrue(zone.TryGetComponent(out BoxCollider collider));
                Assert.IsTrue(collider.isTrigger);
            }

            Assert.AreEqual(25, zones.Length);
            Assert.AreEqual(25, zoneIds.Count);
            Assert.Contains("grid_r00_c00", new List<string>(zoneIds));
            Assert.Contains("grid_r04_c04", new List<string>(zoneIds));
        }

        [Test]
        public void TryGetZoneIdForWorldPositionResolvesCellsAndRejectsOutside()
        {
            AttentionGridZoneGenerator generator = CreateGenerator(5, 5);

            Assert.IsTrue(generator.TryGetZoneIdForWorldPosition(new Vector3(-4.9f, 1f, -4.9f), out string lowerLeftZoneId));
            Assert.AreEqual("grid_r00_c00", lowerLeftZoneId);

            Assert.IsTrue(generator.TryGetZoneIdForWorldPosition(new Vector3(4.9f, 1f, 4.9f), out string upperRightZoneId));
            Assert.AreEqual("grid_r04_c04", upperRightZoneId);

            Assert.IsFalse(generator.TryGetZoneIdForWorldPosition(new Vector3(5.1f, 1f, 0f), out string outsideZoneId));
            Assert.IsEmpty(outsideZoneId);
        }

        [Test]
        public void ChangingRowsAndColumnsChangesResolvedZoneIds()
        {
            AttentionGridZoneGenerator generator = CreateGenerator(2, 3);

            Assert.IsTrue(generator.TryGetZoneIdForWorldPosition(new Vector3(4.9f, 1f, 4.9f), out string zoneId));
            Assert.AreEqual("grid_r02_c01", zoneId);
        }

        [Test]
        public void PlaygroundSceneHasSingleConfiguredGridAndNoActiveManualZones()
        {
            ValidateSceneGrid("Assets/Scenes/Playground.unity");
        }

        [Test]
        public void VisualizationSceneHasSingleConfiguredGridAndNoActiveManualZones()
        {
            ValidateSceneGrid("Assets/Scenes/Visualization.unity");
        }

        [Test]
        public void AttentionSampleSerializesPlayerZoneId()
        {
            AttentionSampleLine sample = new()
            {
                sessionId = "session_001",
                trialId = "trial_001",
                playerZoneId = "grid_r02_c03"
            };

            string json = JsonUtility.ToJson(sample);
            AttentionSampleLine roundTripped = JsonUtility.FromJson<AttentionSampleLine>(json);

            Assert.AreEqual("grid_r02_c03", roundTripped.playerZoneId);
        }

        private AttentionGridZoneGenerator CreateGenerator(int columns, int rows)
        {
            GameObject gridObject = new("Test Attention Grid");
            createdObjects.Add(gridObject);

            AttentionGridZoneGenerator generator = gridObject.AddComponent<AttentionGridZoneGenerator>();
            generator.Configure(columns, rows, Vector3.up, new Vector2(10f, 10f), 2f);
            return generator;
        }

        private static void ValidateSceneGrid(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                List<AttentionGridZoneGenerator> generators = new();
                List<AttentionZone> activeZones = new();

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (!root.activeInHierarchy)
                    {
                        continue;
                    }

                    generators.AddRange(root.GetComponentsInChildren<AttentionGridZoneGenerator>(false));
                    activeZones.AddRange(root.GetComponentsInChildren<AttentionZone>(false));
                }

                Assert.AreEqual(1, generators.Count);
                Assert.AreEqual(5, generators[0].Columns);
                Assert.AreEqual(5, generators[0].Rows);
                Assert.AreEqual(new Vector3(-20f, 3.5f, -15f), generators[0].Center);
                Assert.AreEqual(new Vector2(60f, 60f), generators[0].Size);
                Assert.AreEqual(8f, generators[0].Height);
                Assert.AreEqual(0, activeZones.Count);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}

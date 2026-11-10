using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Squirrel.Combat;
using Squirrel.Edge;
using Squirrel.World;

namespace Squirrel.Tests
{
    /// <summary>
    /// Тесты Мшистой колыбели (MossCradle.unity): кора и камень, святилище, выбор с гусеницей,
    /// сохранение в гнезде, камера по комнатам. Сохранения пишутся во временную папку.
    /// </summary>
    public class MossCradleTests
    {
        const string Scene = "MossCradle";

        SquirrelController squirrel;
        SquirrelInput input;
        SquirrelAbilities abilities;
        SquirrelHealth health;
        EdgeZone zone;
        WorldState world;
        GameSession session;

        [UnitySetUp]
        public IEnumerator Load()
        {
            HitStop.Enabled = false;
            SaveSystem.DirectoryOverride = Path.Combine(Application.temporaryCachePath, "squirrel-tests");
            SaveSystem.Delete("slot0");
            yield return LoadScene();
        }

        IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            squirrel = Object.FindAnyObjectByType<SquirrelController>();
            input = squirrel.GetComponent<SquirrelInput>();
            abilities = squirrel.GetComponent<SquirrelAbilities>();
            health = squirrel.GetComponent<SquirrelHealth>();
            zone = Object.FindAnyObjectByType<EdgeZone>();
            world = WorldState.Current;
            session = Object.FindAnyObjectByType<GameSession>();
            input.SetOverride(Vector2.zero, false);
            yield return WaitSeconds(0.2f);
        }

        [TearDown]
        public void Cleanup()
        {
            SaveSystem.Delete("slot0");
            SaveSystem.DirectoryOverride = null;
            HitStop.Enabled = true;
            Time.timeScale = 1f;
        }

        static IEnumerator WaitSeconds(float t)
        {
            float end = Time.time + t;
            while (Time.time < end) yield return null;
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        void Place(Vector2 p) => squirrel.Teleport(p);

        static T Named<T>(string part) where T : Component
        {
            foreach (var c in Object.FindObjectsByType<T>())
                if (c.name.Contains(part)) return c;
            Assert.Fail($"Нет {typeof(T).Name} с «{part}» в имени");
            return null;
        }

        static FlagSwitch SwitchFor(string flag)
        {
            foreach (var s in Object.FindObjectsByType<FlagSwitch>())
                if (s.Flag == flag) return s;
            Assert.Fail("Нет FlagSwitch для " + flag);
            return null;
        }

        [UnityTest]
        public IEnumerator Location_Has_Rooms_Nests_And_Memories()
        {
            yield return null;
            Assert.AreEqual(15, Room.All.Count, "комнат");
            Assert.AreEqual(2, Object.FindObjectsByType<Nest>().Length, "гнёзд");
            Assert.AreEqual(5, Object.FindObjectsByType<Memory>().Length, "воспоминаний");
            Assert.IsNotNull(Object.FindAnyObjectByType<AbilityPickup>());
            Assert.IsNotNull(Object.FindAnyObjectByType<Caterpillar>());
            Assert.IsNotNull(world, "в локации есть состояние мира");
            Assert.IsFalse(abilities.WallJump, "в начале отталкивания нет");
        }

        [UnityTest]
        public IEnumerator Stone_Wall_Needs_WallJump_Bark_Does_Not()
        {
            // Каменный колодец R5: стенки x=72 и x=77.
            Vector2 inWell = new Vector2(73.5f, 42f);
            Assert.AreEqual("R5", Room.At(inWell).Id);
            Place(inWell);
            input.SetOverride(Vector2.left, false);
            bool clung = false;
            float end = Time.time + 0.3f;
            while (Time.time < end) { clung |= squirrel.State == SquirrelState.OnWall; yield return null; }
            Assert.IsFalse(clung, "за камень без способности не уцепиться");

            abilities.Set(Ability.WallJump, true);
            Place(inWell);
            end = Time.time + 0.3f;
            while (Time.time < end && !clung) { clung = squirrel.State == SquirrelState.OnWall; yield return null; }
            Assert.IsTrue(clung, "с отталкиванием — цепляется и за камень");

            // Кора: ствол в Колыбели (x=24..25) — лезем без способности.
            abilities.Set(Ability.WallJump, false);
            Place(new Vector2(23.5f, 24f));
            input.SetOverride(new Vector2(1f, 1f), false);
            float y0 = squirrel.transform.position.y;
            yield return WaitSeconds(0.6f);
            Assert.AreEqual(SquirrelState.OnWall, squirrel.State, "на коре");
            Assert.Greater(squirrel.transform.position.y, y0 + 1f, "лезет вверх по коре");
            input.SetOverride(Vector2.zero, false);
        }

        [UnityTest]
        public IEnumerator Shrine_Unlocks_WallJump()
        {
            var shrine = Object.FindAnyObjectByType<AbilityPickup>();
            Place((Vector2)shrine.transform.position - new Vector2(0f, 0.35f));
            yield return Frames(3);
            Assert.IsTrue(shrine.Taken);
            Assert.IsTrue(abilities.WallJump);
            Assert.IsTrue(world.Has(SquirrelAbilities.FlagFor(Ability.WallJump)));
        }

        [UnityTest]
        public IEnumerator Freeing_Caterpillar_Weaves_Bridge()
        {
            var rock = Object.FindAnyObjectByType<BreakableRock>();
            var stash = Object.FindAnyObjectByType<StashPile>();
            var bridge = SwitchFor(Caterpillar.HelpedFlag);
            var cocoon = SwitchFor(Caterpillar.RobbedFlag);
            Assert.IsFalse(bridge.Target.activeSelf, "моста пока нет");

            for (int i = 0; i < 3; i++)
                rock.TakeHit(new HitInfo { damage = 1, direction = Vector2.right, source = HitSource.Tail });
            yield return null;

            Assert.IsTrue(rock.Broken);
            Assert.IsTrue(world.Has(Caterpillar.HelpedFlag));
            Assert.AreEqual(1f, world.Compassion, 0.001f);
            Assert.IsTrue(bridge.Target.activeSelf, "мост через расщелину появился");
            Assert.IsFalse(cocoon.Target.activeSelf);
            Assert.IsTrue(stash.Taken, "она забрала запас с собой");
            Assert.IsFalse(stash.TakeHit(new HitInfo { damage = 1 }), "второй выбор закрыт");
        }

        [UnityTest]
        public IEnumerator Robbing_Caterpillar_Gives_A_Seed()
        {
            var rock = Object.FindAnyObjectByType<BreakableRock>();
            var stash = Object.FindAnyObjectByType<StashPile>();
            int max0 = health.Max;

            Assert.IsTrue(stash.TakeHit(new HitInfo { damage = 1, direction = Vector2.right, source = HitSource.Tail }));
            yield return null;

            Assert.IsTrue(world.Has(Caterpillar.RobbedFlag));
            Assert.AreEqual(-1f, world.Compassion, 0.001f);
            Assert.AreEqual(max0 + 1, health.Max, "+1 зерно");
            Assert.AreEqual(health.Settings.maxSap, health.Sap, "полный сок");
            Assert.IsFalse(rock.TakeHit(new HitInfo { damage = 1 }), "камень уже не разбить");
            Assert.IsFalse(SwitchFor(Caterpillar.HelpedFlag).Target.activeSelf, "моста нет");
            Assert.IsTrue(SwitchFor(Caterpillar.RobbedFlag).Target.activeSelf, "засохший кокон у расщелины");
        }

        [UnityTest]
        public IEnumerator Nest_Saves_And_Reload_Restores_Progress()
        {
            // Прогресс: способность, воспоминание, освобождённая гусеница, сдвинутый Край.
            var shrine = Object.FindAnyObjectByType<AbilityPickup>();
            Place((Vector2)shrine.transform.position - new Vector2(0f, 0.35f));
            yield return Frames(3);

            var memory = Named<Memory>("(R1)");
            Place((Vector2)memory.transform.position - new Vector2(0f, 0.35f));
            yield return Frames(3);
            Assert.IsTrue(memory.Collected);

            var rock = Object.FindAnyObjectByType<BreakableRock>();
            for (int i = 0; i < 3; i++) rock.TakeHit(new HitInfo { damage = 1, direction = Vector2.right });

            zone.DebugSkipStage();
            yield return Frames(2);
            Assert.AreEqual(EdgeStage.Shrink, zone.Stage);
            zone.DebugSkipStage();
            yield return Frames(2);
            Assert.AreEqual(1, zone.Phase);

            zone.DebugSetFront(0, 10f);
            var nest = Named<Nest>("(R3)");
            Place(nest.SpawnPoint);
            yield return Frames(3);
            Assert.IsTrue(SaveSystem.Exists(session.Slot), "в гнезде игра сохранилась");
            float pushed = zone.PushedBack;
            float front0 = zone.FrontRadius(0);
            Assert.Greater(pushed, 0f);

            // Перезапуск сцены — как выход из игры и вход снова.
            yield return LoadScene();
            Assert.IsTrue(session.LoadedFromSave);
            Assert.Less(Vector2.Distance(squirrel.transform.position, Named<Nest>("(R3)").SpawnPoint), 0.6f, "в том же гнезде");
            Assert.IsTrue(abilities.WallJump, "способность сохранилась");
            Assert.IsTrue(world.Has(Caterpillar.HelpedFlag));
            Assert.AreEqual(1f, world.Compassion, 0.001f);
            Assert.IsTrue(Named<Memory>("(R1)").Collected, "воспоминание не появилось снова");
            Assert.IsTrue(Object.FindAnyObjectByType<BreakableRock>().Broken);
            Assert.IsTrue(SwitchFor(Caterpillar.HelpedFlag).Target.activeSelf, "мост на месте");
            Assert.AreEqual(1, zone.Phase, "фаза Края");
            Assert.AreEqual(pushed, zone.PushedBack, 0.001f, "отталкивание Края");
            Assert.AreEqual(front0, zone.FrontRadius(0), 0.05f, "очаг Края");
            Assert.IsTrue(Object.FindAnyObjectByType<AbilityPickup>().Taken);
        }

        int FrontNear(Vector2 p)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < zone.Field.FrontCount; i++)
            {
                float d = Vector2.Distance(zone.Field.Sources[i], p);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        [UnityTest]
        public IEnumerator Edge_Measures_Distance_Along_Passages()
        {
            yield return null;
            Assert.IsTrue(zone.Field != null && zone.Field.Ready, "у локации есть поле Края");
            Assert.AreEqual(5, zone.FrontCount, "очагов Края");
            Vector2 heart = Object.FindAnyObjectByType<Heart>().transform.position;
            // Верхние кроны (R14) по прямой рядом с Сердцем, но за стеной: идти до них далеко.
            Vector2 crowns = Named<Memory>("(R14)").transform.position;
            Assert.Greater(zone.DistanceToCenter(crowns), Vector2.Distance(crowns, heart) * 2f, "расстояние меряется по проходам");
            // В начале весь мир внутри Края.
            foreach (var m in Object.FindObjectsByType<Memory>())
                Assert.Greater(zone.SignedDistanceInside(m.transform.position), 0f, m.name + " внутри");
            Assert.Greater(zone.SignedDistanceInside(squirrel.transform.position), 0f, "белка внутри");
        }

        [UnityTest]
        public IEnumerator Front_Grows_Only_Near_The_Squirrel()
        {
            int near = FrontNear(new Vector2(2.5f, 51.5f));   // Дупло (R15)
            int far = FrontNear(new Vector2(157.5f, 69.5f));  // Зал плевунов (R10)
            Place(new Vector2(5.5f, 37.05f));                  // пол Дупла
            yield return WaitSeconds(0.3f);
            float n0 = zone.FrontRadius(near), f0 = zone.FrontRadius(far);
            yield return WaitSeconds(1.5f);
            Assert.IsTrue(zone.IsFrontPressing(near), "ближний очаг давит");
            Assert.Greater(zone.FrontRadius(near), n0 + 0.4f, "ближний очаг расползается");
            Assert.IsFalse(zone.IsFrontPressing(far), "дальний очаг спит");
            Assert.AreEqual(f0, zone.FrontRadius(far), 0.001f, "дальний очаг стоит на месте");

            // В гнезде очаги замирают.
            var nest = Named<Nest>("(R3)");
            Place(nest.SpawnPoint);
            yield return Frames(3);
            float all0 = 0f;
            for (int i = 0; i < zone.FrontCount; i++) all0 += zone.FrontRadius(i);
            yield return WaitSeconds(0.5f);
            float all1 = 0f;
            for (int i = 0; i < zone.FrontCount; i++) all1 += zone.FrontRadius(i);
            Assert.AreEqual(all0, all1, 0.001f, "в гнезде Край стоит");
        }

        [UnityTest]
        public IEnumerator Fronts_Never_Swallow_Heart_Or_Nests()
        {
            yield return null;
            for (int i = 0; i < zone.FrontCount; i++) zone.DebugSetFront(i, 10000f);
            var s = zone.Settings;
            Vector2 heart = Object.FindAnyObjectByType<Heart>().transform.position;
            Assert.GreaterOrEqual(zone.SignedDistanceInside(heart), s.frontHeartSafe - 0.5f, "у Сердца всегда есть место");
            foreach (var nest in Object.FindObjectsByType<Nest>())
                Assert.GreaterOrEqual(zone.SignedDistanceInside((Vector2)nest.transform.position + Vector2.up * 0.5f),
                    s.frontNestSafe - 0.5f, nest.name + " остаётся укрытием");
        }

        [UnityTest]
        public IEnumerator Memory_Pushes_Nearest_Front_Back()
        {
            int near = FrontNear(new Vector2(2.5f, 51.5f));
            zone.DebugSetFront(near, 8f);
            var memory = Named<Memory>("(R15)");
            Place((Vector2)memory.transform.position - new Vector2(0f, 0.35f));
            yield return Frames(3);
            Assert.IsTrue(memory.Collected);
            yield return WaitSeconds(2f);
            Assert.Less(zone.FrontRadius(near), 5f, "очаг в Дупле отступил");
        }

        [UnityTest]
        public IEnumerator Camera_Stays_Inside_Current_Room()
        {
            var r2 = Named<Room>("R2");
            Place(new Vector2(r2.Bounds.xMin + 3f, r2.Bounds.yMin + 1.05f));
            yield return WaitSeconds(1.2f);
            var tracker = squirrel.GetComponent<RoomTracker>();
            Assert.AreEqual("R2", tracker.Current.Id);
            var cam = Camera.main;
            float halfW = cam.orthographicSize * cam.aspect;
            Assert.GreaterOrEqual(cam.transform.position.x - halfW, r2.Bounds.xMin - 0.3f, "камера не заглядывает в соседнюю комнату");
        }
    }
}

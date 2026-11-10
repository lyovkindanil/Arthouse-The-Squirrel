using System.Collections;
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
    /// Дымовые тесты серой комнаты: грузят Greybox.unity и гоняют белку через SquirrelInput.SetOverride.
    /// Ассеты настроек не меняются.
    /// </summary>
    public class GreyboxSmokeTests
    {
        SquirrelController squirrel;
        SquirrelInput input;
        EdgeZone zone;
        EdgeExposure exposure;
        SquirrelHealth health;
        Respawner respawner;
        Heart heart;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            yield return null;
            squirrel = Object.FindAnyObjectByType<SquirrelController>();
            input = squirrel.GetComponent<SquirrelInput>();
            exposure = squirrel.GetComponent<EdgeExposure>();
            health = squirrel.GetComponent<SquirrelHealth>();
            respawner = squirrel.GetComponent<Respawner>();
            zone = Object.FindAnyObjectByType<EdgeZone>();
            heart = Object.FindAnyObjectByType<Heart>();
            input.SetOverride(Vector2.zero, false);
            yield return WaitSeconds(0.3f); // белка встаёт на пол
        }

        static IEnumerator WaitSeconds(float t)
        {
            float end = Time.time + t;
            while (Time.time < end) yield return null;
        }

        void Place(float x, float y) => squirrel.Teleport(new Vector2(x, y));

        Nest FindNest(string prefix)
        {
            foreach (var n in Object.FindObjectsByType<Nest>())
                if (n.name.StartsWith(prefix)) return n;
            Assert.Fail("Нет гнезда " + prefix);
            return null;
        }

        [UnityTest]
        public IEnumerator Runs_Right_On_Ground()
        {
            Assert.AreEqual(SquirrelState.Grounded, squirrel.State, "белка должна стоять на полу");
            float x0 = squirrel.transform.position.x;
            input.SetOverride(Vector2.right, false);
            yield return WaitSeconds(0.6f);
            Assert.AreEqual(SquirrelState.Grounded, squirrel.State);
            Assert.AreEqual(squirrel.Settings.maxRunSpeed, squirrel.Velocity.x, 0.01f, "вышла на полную скорость");
            Assert.Greater(squirrel.transform.position.x - x0, 3f);
        }

        [UnityTest]
        public IEnumerator Jump_Reaches_Configured_Height_And_Lands()
        {
            float y0 = squirrel.transform.position.y, maxY = y0;
            input.SetOverride(Vector2.zero, true);
            float end = Time.time + 1.5f;
            while (Time.time < end)
            {
                maxY = Mathf.Max(maxY, squirrel.transform.position.y);
                yield return null;
            }
            input.SetOverride(Vector2.zero, false);
            yield return WaitSeconds(0.5f);
            Assert.AreEqual(squirrel.Settings.jumpHeight, maxY - y0, 0.4f, "высота полного прыжка");
            Assert.AreEqual(SquirrelState.Grounded, squirrel.State, "приземлилась");
            Assert.AreEqual(y0, squirrel.transform.position.y, 0.05f);
        }

        [UnityTest]
        public IEnumerator Climbs_The_Trunk()
        {
            Place(-8f, 0.05f); // слева от ствола (x -6..-4)
            yield return null;
            input.SetOverride(Vector2.right, false);
            yield return WaitSeconds(0.4f);
            input.SetOverride(new Vector2(1f, 1f), true); // прыжок к стене, жмём к ней и вверх
            bool wasOnWall = false;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                wasOnWall |= squirrel.State == SquirrelState.OnWall;
                yield return null;
            }
            Assert.IsTrue(wasOnWall, "зацепилась за кору");
            Assert.Greater(squirrel.transform.position.y, 6f, "лезет вверх по стволу");
            Assert.Less(squirrel.transform.position.x, -5.5f, "не прошла сквозь ствол");
        }

        [UnityTest]
        public IEnumerator Edge_Shrinks_In_Shorter_Phases()
        {
            Place(-20f, 0.05f); // вне гнезда, иначе Край стоит
            yield return null;
            var s = zone.Settings;
            Assert.AreEqual(EdgeStage.Hold, zone.Stage);
            float prevRadius = zone.Radius, prevHold = zone.StageDuration;
            Assert.AreEqual(s.HoldTime(0), prevHold, 0.001f);

            for (int phase = 1; phase < s.phaseCount; phase++)
            {
                zone.DebugSkipStage();
                yield return null;
                yield return null;
                Assert.AreEqual(EdgeStage.Shrink, zone.Stage, "после паузы Край ползёт");
                zone.DebugSkipStage();
                yield return null;
                yield return null;
                Assert.AreEqual(EdgeStage.Hold, zone.Stage);
                Assert.AreEqual(phase, zone.Phase);
                Assert.AreEqual(s.RadiusAt(phase), zone.Radius, 0.01f);
                Assert.Less(zone.Radius, prevRadius, "зона сжалась");
                Assert.Less(zone.StageDuration, prevHold, "каждая пауза короче");
                prevRadius = zone.Radius;
                prevHold = zone.StageDuration;
            }
        }

        [UnityTest]
        public IEnumerator Edge_Pauses_While_In_Nest()
        {
            // Белка стартует в гнезде 1.
            Assert.IsTrue(zone.IsPaused);
            float left = zone.StageTimeLeft;
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(left, zone.StageTimeLeft, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Exposure_Outside_Edge_Returns_To_Nest_And_Rolls_Back()
        {
            var nest2 = FindNest("Nest 2");
            Place(nest2.SpawnPoint.x, nest2.SpawnPoint.y);
            yield return WaitSeconds(0.2f);
            Assert.AreSame(nest2, Nest.LastVisited, "гнездо 2 запомнено");

            Place(-20f, 0.05f);
            yield return null;
            // Перематываем до паузы фазы 2: радиус ~33, левый угол комнаты уже за Краем.
            while (zone.Phase < 2)
            {
                zone.DebugSkipStage();
                yield return null;
            }
            Assert.AreEqual(EdgeStage.Hold, zone.Stage);

            int fell = 0;
            respawner.FellDeeper += _ => fell++;
            Place(-30f, 0.05f);
            yield return null;
            Assert.IsTrue(exposure.IsOutside, "белка за Краем");

            float end = Time.time + health.Max / zone.Settings.drainPerSecond + 2f;
            while (fell == 0 && Time.time < end) yield return null;

            yield return null; // IsOutside пересчитывается в следующем кадре
            Assert.AreEqual(1, fell, "провал глубже в сон");
            Assert.AreEqual(1, respawner.SleepDepth);
            Assert.AreEqual(1, zone.Phase, "Край откатился на фазу");
            Assert.AreEqual(EdgeStage.Hold, zone.Stage);
            Assert.Less(Vector2.Distance(squirrel.transform.position, nest2.SpawnPoint), 0.5f, "вернулась в гнездо 2");
            Assert.AreEqual(health.Max, health.Current, 0.001f);
            Assert.IsFalse(exposure.IsOutside);
        }

        [UnityTest]
        public IEnumerator Touching_Heart_Stabilizes_Edge()
        {
            Place(heart.transform.position.x, 28.35f); // на уступе под Сердцем
            yield return WaitSeconds(0.2f);
            Assert.IsTrue(heart.Restored);
            Assert.AreEqual(EdgeStage.Stabilized, zone.Stage);

            float r = zone.Radius;
            zone.DebugSkipStage();
            yield return WaitSeconds(0.2f);
            Assert.AreEqual(r, zone.Radius, 0.0001f, "Край больше не двигается");
        }

        [UnityTest]
        public IEnumerator Edge_Drain_Melts_Seeds_And_Only_Partial_Seed_Regrows()
        {
            Place(-20f, 0.05f);
            yield return null;
            while (zone.Phase < 2) { zone.DebugSkipStage(); yield return null; }
            Place(-30f, 0.05f); // за Краем
            float end = Time.time + 1.2f / zone.Settings.drainPerSecond; // ~1.2 зерна
            while (Time.time < end) yield return null;
            Assert.Less(health.Current, health.Max - 1f, "растаяло больше одного зерна");
            float melted = health.Current;

            Place(14f, 20.55f); // обратно в зону: платформа D рядом с Сердцем (гнездо не трогаем — оно лечит)
            yield return WaitSeconds(zone.Settings.regenDelay + 2.5f);
            Assert.IsFalse(exposure.IsOutside);
            Assert.AreEqual(Mathf.Ceil(melted), health.Current, 0.001f, "вернулось только подтаявшее зерно");
        }

        [UnityTest]
        public IEnumerator Memory_Pushes_Edge_Back()
        {
            Memory memory = null;
            foreach (var m in Object.FindObjectsByType<Memory>())
                if (m.name.StartsWith("Memory 1")) memory = m;
            Assert.IsNotNull(memory, "в комнате есть воспоминание");

            Place(-20f, 0.05f);
            yield return null;
            float r0 = zone.Radius;
            Place(memory.transform.position.x, memory.transform.position.y - 0.35f);
            yield return null;
            yield return null;
            Assert.IsTrue(memory.Collected, "белка подобрала воспоминание");
            yield return WaitSeconds(memory.PushBackAmount / 3f + 0.5f);
            Assert.AreEqual(r0 + memory.PushBackAmount, zone.Radius, 0.05f, "Край отступил");
            Assert.AreEqual(memory.PushBackAmount, zone.PushedBack, 0.001f);
        }
    }
}

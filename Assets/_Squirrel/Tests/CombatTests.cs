using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Squirrel.Combat;
using Squirrel.Enemies;
using Squirrel.World;

namespace Squirrel.Tests
{
    /// <summary>
    /// Тесты боя на арене (Arena.unity): удар хвостом, отскок, жёлудь, урон, лечение, смерть и возврат.
    /// В каждом тесте включены только нужные враги, остальные выключены.
    /// </summary>
    public class CombatTests
    {
        SquirrelController squirrel;
        SquirrelInput input;
        SquirrelHealth health;
        AcornThrower thrower;
        Respawner respawner;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            HitStop.Enabled = false; // стоп-кадры только мешают считать время
            yield return SceneManager.LoadSceneAsync("Arena", LoadSceneMode.Single);
            yield return null;
            squirrel = Object.FindAnyObjectByType<SquirrelController>();
            input = squirrel.GetComponent<SquirrelInput>();
            health = squirrel.GetComponent<SquirrelHealth>();
            thrower = squirrel.GetComponent<AcornThrower>();
            respawner = squirrel.GetComponent<Respawner>();
            input.SetOverride(Vector2.zero, false);
            yield return WaitSeconds(0.3f);
        }

        [TearDown]
        public void Restore()
        {
            HitStop.Enabled = true;
            Time.timeScale = 1f;
        }

        static IEnumerator WaitSeconds(float t)
        {
            float end = Time.time + t;
            while (Time.time < end) yield return null;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        void Place(float x, float y) => squirrel.Teleport(new Vector2(x, y));

        /// <summary>Оставить включённым только врага с таким началом имени.</summary>
        T Only<T>(string prefix) where T : Enemy
        {
            T keep = null;
            foreach (var e in Object.FindObjectsByType<Enemy>())
            {
                bool k = prefix != null && e.name.StartsWith(prefix);
                e.gameObject.SetActive(k);
                if (k) keep = (T)e;
            }
            if (prefix != null) Assert.IsNotNull(keep, "нет врага " + prefix);
            return keep;
        }

        IEnumerator Press(Vector2 move, bool attack = false, bool throwAcorn = false)
        {
            input.SetOverride(move, false, attack, throwAcorn);
            yield return null;
            input.SetOverride(move, false, false, false);
        }

        [UnityTest]
        public IEnumerator Tail_Hit_Lands_On_Dummy_And_Gives_Sap()
        {
            var dummy = Only<TrainingDummy>("Training Dummy");
            Place(dummy.transform.position.x - 1.3f, 0.05f); // слева от пня, смотрит вправо
            yield return Frames(2);
            Assert.AreEqual(1, squirrel.Facing);
            Assert.AreEqual(0, health.Sap);

            yield return Press(Vector2.zero, attack: true);
            yield return Frames(3);
            Assert.AreEqual(1, dummy.HitsTaken, "попала хвостом");
            Assert.AreEqual(health.Settings.sapPerHit, health.Sap, "сок за попадание");

            // Повторное нажатие до перезарядки не бьёт второй раз за тот же взмах.
            yield return WaitSeconds(health.Settings.tailCooldown + 0.05f);
            yield return Press(Vector2.zero, attack: true);
            yield return Frames(3);
            Assert.AreEqual(2, dummy.HitsTaken);
        }

        [UnityTest]
        public IEnumerator Down_Strike_Bounces_Off_Enemy()
        {
            var dummy = Only<TrainingDummy>("Training Dummy");
            Place(dummy.transform.position.x, 2.2f); // в воздухе над пнём
            yield return null;
            input.SetOverride(Vector2.down, false, true);
            float maxVy = float.MinValue;
            float end = Time.time + 0.2f;
            while (Time.time < end)
            {
                maxVy = Mathf.Max(maxVy, squirrel.Velocity.y);
                yield return null;
            }
            input.SetOverride(Vector2.zero, false);
            Assert.AreEqual(1, dummy.HitsTaken, "удар вниз попал");
            Assert.Greater(maxVy, health.Settings.pogoVelocity * 0.7f, "отскок вверх");
        }

        [UnityTest]
        public IEnumerator Crawler_Dies_After_Enough_Hits()
        {
            var crawler = Only<Crawler>("Crawler A");
            for (int i = 0; i < crawler.MaxHp; i++)
                crawler.TakeHit(new HitInfo { damage = 1, direction = Vector2.right, source = HitSource.Tail });
            yield return null;
            Assert.IsTrue(crawler.IsDead);
            Assert.IsFalse(crawler.CanBeHit);
        }

        [UnityTest]
        public IEnumerator Touching_Crawler_Hurts_Once_Then_Invulnerable()
        {
            var crawler = Only<Crawler>("Crawler A");
            yield return Frames(2);
            Place(crawler.transform.position.x, 0.05f);
            yield return Frames(3);
            Assert.AreEqual(health.Max - 1, health.Current, 0.001f, "касание сняло одно зерно");
            Assert.IsTrue(health.IsInvulnerable);

            Place(crawler.transform.position.x, 0.05f); // снова в моховика, пока мигает
            yield return Frames(3);
            Assert.AreEqual(health.Max - 1, health.Current, 0.001f, "неуязвимость защищает");
        }

        [UnityTest]
        public IEnumerator Acorn_Hits_At_Range_And_Recharges()
        {
            var dummy = Only<TrainingDummy>("Training Dummy");
            Place(dummy.transform.position.x - 6f, 0.05f);
            yield return Frames(2);
            int ammo = thrower.Ammo;
            Assert.AreEqual(thrower.MaxAmmo, ammo);

            yield return Press(Vector2.zero, throwAcorn: true);
            Assert.AreEqual(ammo - 1, thrower.Ammo, "жёлудь потрачен");
            yield return WaitSeconds(0.6f);
            Assert.AreEqual(1, dummy.HitsTaken, "жёлудь долетел");
            Assert.AreEqual(0, health.Sap, "жёлуди сока не дают");

            yield return WaitSeconds(health.Settings.acornRecharge + 0.2f);
            Assert.AreEqual(ammo, thrower.Ammo, "жёлудь восстановился");
        }

        [UnityTest]
        public IEnumerator Healing_Spends_Sap_And_Restores_A_Seed()
        {
            Only<Enemy>(null);
            Place(-24f, 0.05f);
            yield return WaitSeconds(0.2f);
            var s = health.Settings;
            health.DebugSet(3f, s.healCost);

            input.SetOverride(Vector2.right, false, false, false, true); // держим лечение и жмём «бег»
            yield return Frames(2);
            float x0 = squirrel.transform.position.x;
            Assert.IsTrue(health.IsHealing);
            Assert.IsTrue(squirrel.Rooted);
            yield return WaitSeconds(s.healTime * 0.6f);
            Assert.AreEqual(x0, squirrel.transform.position.x, 0.05f, "во время лечения белка стоит");
            input.SetOverride(Vector2.zero, false, false, false, true);
            yield return WaitSeconds(s.healTime * 0.5f);
            input.SetOverride(Vector2.zero, false);
            yield return null;

            Assert.AreEqual(4f, health.Current, 0.001f, "зерно вернулось");
            Assert.AreEqual(0, health.Sap, "сок потрачен");
            Assert.IsFalse(squirrel.Rooted);
        }

        [UnityTest]
        public IEnumerator Death_In_Combat_Returns_To_Nest_And_Resets_Enemies()
        {
            var crawler = Only<Crawler>("Crawler A");
            crawler.TakeHit(new HitInfo { damage = crawler.MaxHp, direction = Vector2.right, source = HitSource.Tail });
            Assert.IsTrue(crawler.IsDead);

            Place(-24f, 0.05f);
            yield return Frames(2);
            health.DebugSet(1f, 0);
            Assert.IsTrue(health.Damage(1, squirrel.transform.position + Vector3.right));
            yield return Frames(2);

            var nest = Nest.LastVisited;
            Assert.IsNotNull(nest, "стартовое гнездо запомнено");
            Assert.Less(Vector2.Distance(squirrel.transform.position, nest.SpawnPoint), 0.5f, "вернулась в гнездо");
            Assert.AreEqual(health.Max, health.Current, 0.001f);
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(1, respawner.Deaths);
            Assert.AreEqual(0, respawner.SleepDepth, "смерть в бою не углубляет сон");
            Assert.IsFalse(crawler.IsDead, "враги вернулись");
            Assert.AreEqual(crawler.MaxHp, crawler.Hp);
        }

        [UnityTest]
        public IEnumerator Moth_Swoops_At_Squirrel()
        {
            var moth = Only<Moth>("Moth");
            Place(10f, 8.55f); // на платформе P2, под молью
            float end = Time.time + 4f;
            bool swooped = false;
            while (Time.time < end && health.Current >= health.Max)
            {
                swooped |= moth.State == Moth.MothState.Swoop;
                yield return null;
            }
            Assert.IsTrue(swooped, "моль спикировала");
            Assert.Less(health.Current, health.Max, "и задела белку");
        }

        [UnityTest]
        public IEnumerator Spitter_Shoots_Spore_That_Hurts()
        {
            var spitter = Only<Spitter>("Spitter");
            Place(18f, 4.55f); // на той же платформе P3
            float end = Time.time + 5f;
            while (Time.time < end && health.Current >= health.Max) yield return null;
            Assert.Greater(spitter.ShotsFired, 0, "плевун выстрелил");
            Assert.Less(health.Current, health.Max, "спора попала");
        }

        [UnityTest]
        public IEnumerator Tail_Destroys_Spore_Without_Sap()
        {
            Only<Enemy>(null);
            Place(-24f, 0.05f);
            yield return Frames(2);
            var spore = SporeShot.Spawn(squirrel.transform.position + new Vector3(3f, 0.35f), Vector2.left * 4f, null, null);
            yield return WaitSeconds(0.35f); // спора подлетает на дистанцию удара
            yield return Press(Vector2.zero, attack: true);
            yield return Frames(2);
            Assert.IsTrue(spore == null, "спору сбили");
            Assert.AreEqual(health.Max, health.Current, 0.001f, "белка цела");
            Assert.AreEqual(0, health.Sap);
        }
    }
}

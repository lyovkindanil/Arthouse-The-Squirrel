using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Squirrel.Combat;
using Squirrel.Enemies;
using Squirrel.World;

namespace Squirrel.Tests
{
    /// <summary>Босс Мшистой колыбели: арена, ворота, уязвимость, вторая фаза, Сердце, сохранение.</summary>
    public class RootWardenTests
    {
        const string Scene = "MossCradle";
        static readonly Vector2 ArenaFloor = new Vector2(88.5f, 56.05f);

        SquirrelController squirrel;
        SquirrelHealth health;
        RootWarden boss;
        Heart heart;
        GameObject gate;

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
            health = squirrel.GetComponent<SquirrelHealth>();
            boss = Object.FindAnyObjectByType<RootWarden>();
            heart = Object.FindAnyObjectByType<Heart>();
            gate = boss.Gate;
            squirrel.GetComponent<SquirrelInput>().SetOverride(Vector2.zero, false);
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            SaveSystem.Delete("slot0");
            SaveSystem.DirectoryOverride = null;
            HitStop.Enabled = true;
        }

        static IEnumerator WaitSeconds(float t)
        {
            float end = Time.time + t;
            while (Time.time < end) yield return null;
        }

        static HitInfo Hit(int dmg) => new HitInfo { damage = dmg, direction = Vector2.left, source = HitSource.Tail };

        void Kill()
        {
            boss.DebugExpose();
            boss.TakeHit(Hit(boss.MaxHp / 2));      // во вторую фазу
            boss.DebugExpose();
            boss.TakeHit(Hit(boss.MaxHp));
        }

        [UnityTest]
        public IEnumerator Entering_Arena_Wakes_Boss_And_Closes_Gate()
        {
            Assert.AreEqual(WardenState.Dormant, boss.State);
            Assert.IsFalse(gate.activeSelf, "до боя ворота открыты");
            squirrel.Teleport(ArenaFloor);
            yield return WaitSeconds(0.2f);
            Assert.IsTrue(boss.IsFighting, "сторож проснулся");
            Assert.IsTrue(gate.activeSelf, "ворота закрылись");
        }

        [UnityTest]
        public IEnumerator Boss_Is_Hurt_Only_When_Exposed()
        {
            boss.DebugWake();
            yield return null;
            Assert.IsTrue(boss.TakeHit(Hit(1)), "удар отскакивает, но засчитан");
            Assert.AreEqual(boss.MaxHp, boss.Hp, "закрытый сторож не теряет здоровье");
            Assert.IsFalse(boss.GivesSap, "закрытый не даёт сок");
            boss.DebugExpose();
            Assert.IsTrue(boss.GivesSap);
            boss.TakeHit(Hit(1));
            Assert.AreEqual(boss.MaxHp - 1, boss.Hp, "раскрытый получает урон");
        }

        [UnityTest]
        public IEnumerator Half_Health_Starts_Phase_Two()
        {
            boss.DebugWake();
            yield return null;
            Assert.AreEqual(1, boss.Phase);
            boss.DebugExpose();
            boss.TakeHit(Hit(boss.MaxHp / 2));
            Assert.AreEqual(2, boss.Phase);
            Assert.AreEqual(WardenState.Enraging, boss.State, "ярость при переходе");
        }

        [UnityTest]
        public IEnumerator Root_Spikes_Hurt_The_Squirrel()
        {
            squirrel.Teleport(ArenaFloor);
            yield return WaitSeconds(2f); // пробуждение
            float before = health.Current;
            boss.StartAttack(WardenAttack.RootSpikes);
            yield return WaitSeconds(1.6f);
            Assert.Less(health.Current, before, "шип из-под земли ранил белку, стоящую на месте");
        }

        [UnityTest]
        public IEnumerator Heart_Locked_Until_Warden_Falls()
        {
            Vector2 onHeart = (Vector2)heart.transform.position + new Vector2(0f, -0.45f);
            squirrel.Teleport(onHeart);
            yield return WaitSeconds(0.3f);
            Assert.IsTrue(heart.Locked);
            Assert.IsFalse(heart.Restored, "пока сторож жив, Сердце не восстановить");

            Kill();
            yield return null;
            Assert.AreEqual(WardenState.Defeated, boss.State);
            Assert.IsFalse(gate.activeSelf, "ворота открылись");
            Assert.IsTrue(WorldState.HasFlag(RootWarden.DefeatedFlag));
            squirrel.Teleport(onHeart);
            yield return WaitSeconds(0.2f);
            Assert.IsTrue(heart.Restored, "после победы Сердце восстанавливается");
        }

        [UnityTest]
        public IEnumerator Squirrel_Death_Restarts_The_Fight()
        {
            squirrel.Teleport(ArenaFloor);
            yield return WaitSeconds(0.2f);
            boss.DebugExpose();
            boss.TakeHit(Hit(5));
            Assert.AreEqual(boss.MaxHp - 5, boss.Hp);
            health.DebugSet(1f, 0);
            health.Damage(1, squirrel.transform.position);
            yield return WaitSeconds(0.2f);
            Assert.AreEqual(boss.MaxHp, boss.Hp, "здоровье сторожа вернулось");
            Assert.AreEqual(WardenState.Dormant, boss.State, "бой начнётся заново");
            Assert.IsFalse(gate.activeSelf, "ворота открыты");
        }

        [UnityTest]
        public IEnumerator Victory_Is_Saved()
        {
            boss.DebugWake();
            yield return null;
            Kill();
            yield return null;
            Nest nest = null;
            foreach (var n in Object.FindObjectsByType<Nest>()) if (n.name.Contains("(R3)")) nest = n;
            squirrel.Teleport(nest.SpawnPoint);
            yield return WaitSeconds(0.2f);
            Assert.IsTrue(SaveSystem.Exists("slot0"));

            yield return LoadScene();
            Assert.AreEqual(WardenState.Defeated, boss.State, "сторож не воскрес после загрузки");
            Assert.IsFalse(heart.Locked);
            Assert.IsFalse(gate.activeSelf);
        }

        [UnityTest]
        public IEnumerator Warden_Walks_Slowly_Towards_Squirrel_And_Keeps_Distance()
        {
            Vector3 gatePos = gate.transform.position;
            health.GrantInvulnerability(60f); // атаки не мешают смотреть за шагом
            squirrel.Teleport(ArenaFloor);
            yield return WaitSeconds(2f); // пробуждение
            float x0 = boss.transform.position.x;
            yield return WaitSeconds(1f);
            float step = boss.transform.position.x - x0;
            Assert.Greater(step, 0.3f, "идёт к белке");
            Assert.Less(step, 3f, "идёт не быстро");
            Assert.AreEqual(1f, boss.Facing, "смотрит на белку");
            Assert.AreEqual(gatePos, gate.transform.position, "ворота не ходят за сторожем");

            Vector2 near = new Vector2(74.5f, 56.05f); // слева от уступа с Сердцем
            squirrel.Teleport(near);
            yield return WaitSeconds(5f);
            Assert.IsTrue(boss.IsFighting);
            float gap = near.x - boss.transform.position.x;
            Assert.Greater(gap, 4f, "не наступает на белку");
            Assert.Less(gap, 6.5f, "подошёл к белке");
        }

        [UnityTest]
        public IEnumerator Burrow_Moves_Warden_To_Other_Side_Of_Squirrel()
        {
            Vector2 middle = new Vector2(72.5f, 56.05f);
            squirrel.Teleport(middle);
            yield return WaitSeconds(2f);
            Assert.Less(boss.transform.position.x, middle.x, "сначала слева от белки");
            float before = health.Current;
            boss.DebugBurrow();
            yield return WaitSeconds(0.3f);
            Assert.IsTrue(boss.IsUnderground);
            Assert.IsFalse(boss.CanBeHit, "под землёй не достать");

            float end = Time.time + 10f;
            while (Time.time < end && boss.State != WardenState.Idle) yield return null;
            Assert.AreEqual(WardenState.Idle, boss.State, "вынырнул и продолжил бой");
            Assert.Greater(boss.transform.position.x, middle.x + 4f, "вынырнул по другую сторону от белки");
            Assert.AreEqual(before, health.Current, "холмик прошёл под белкой, не ранив");
            Assert.IsTrue(boss.CanBeHit);
        }

        [UnityTest]
        public IEnumerator Death_While_Underground_Resets_Warden_To_Wall()
        {
            Vector3 start = boss.transform.position;
            squirrel.Teleport(ArenaFloor);
            yield return WaitSeconds(2f);
            boss.DebugBurrow();
            yield return WaitSeconds(1.5f);
            health.DebugSet(1f, 0);
            health.Damage(1, squirrel.transform.position);
            yield return WaitSeconds(0.2f);
            Assert.AreEqual(WardenState.Dormant, boss.State);
            Assert.AreEqual(start.x, boss.transform.position.x, 0.01f, "вернулся к стене");
            Assert.IsFalse(boss.IsUnderground);
        }
    }
}

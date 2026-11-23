#!/usr/bin/env python3
"""Gameplay-source contracts for pro gunplay and bot death loot; not native Play Mode."""
from pathlib import Path
import json
import unittest

ROOT = Path(__file__).resolve().parents[2]
DIR = ROOT / "Assets/AarisWorld/Scripts"
def read(name): return (DIR / name).read_text("utf-8")

GUN = read("AarisEquippedGun.cs")
RIVAL = read("AarisOfflineBRDirector.cs")
CRATE = read("AarisDeathCrateWorld.cs")
FX = read("AarisRivalGunfireVfx.cs")
INVENTORY = read("AarisWeaponInventory.cs")
MEDICAL = read("AarisOfflineTacticalHUD.cs")
HUD = read("AarisWeaponHUD.cs")
SPEC = read("AarisWeaponCatalog.cs")
CAT = json.loads((ROOT / "Assets/Resources/AarisWeapons/weapon_catalog.json").read_text("utf-8"))

class ProGunplayAndDeathCrate(unittest.TestCase):
    def test_all_seven_authored_damage_profiles_are_distinct(self):
        guns = {w["id"]: w for w in CAT["weapons"]}
        self.assertEqual(set(guns), {"SMG","AssaultRifle","BattleRifle","Sniper","Shotgun","DMR","Pistol"})
        self.assertEqual(len(set(x["damage"] for x in guns.values())), 7)
        self.assertEqual(len(set(x["rpm"] for x in guns.values())), 7)
        self.assertIn("headshotMultiplier >= 1f && headshotMultiplier <= 4f", SPEC)
        self.assertEqual(guns["Sniper"]["headshotMultiplier"], 2.25)
        self.assertLess(guns["SMG"]["headshotMultiplier"],
                        guns["DMR"]["headshotMultiplier"])
        self.assertIn("ApplyDamage(target.collider, spec.damage, owner, target.point,", GUN)
        self.assertIn("spec.headshotMultiplier);", GUN)
        self.assertIn("victim.ApplyHitDamage(damage, attacker, headshot)", GUN)

    def test_authoritative_firing_gate_and_pooled_player_effects(self):
        firing = GUN.split("public bool TryFire(Camera camera, GameObject owner)",1)[1].split(
            "private static bool IsAuthoredHeadHit",1)[0]
        self.assertLess(firing.index("magazine--;"), firing.index("TriggerMuzzleFlash();"))
        self.assertIn("ScheduleCasingEjection();",firing)
        self.assertIn("TriggerTracer(muzzle, impactPoint);",firing)
        self.assertIn("targetFound = muzzleHitFound;",firing)
        self.assertIn("muzzleLight.shadows = LightShadows.None;",GUN)
        self.assertIn("if (muzzleLight != null) muzzleLight.enabled = false;",GUN)
        self.assertIn("groundY = landing.point.y + 0.012f;",GUN)
        self.assertIn("casingVelocity.y = -casingVelocity.y * 0.28f;",GUN)
        self.assertNotIn("new LineRenderer()",firing)
        self.assertNotIn("GameObject.CreatePrimitive",firing)

    def test_automatic_shells_survive_until_ground_bounce(self):
        import math
        self.assertIn("private const int ExtraShellSlots = 9;", GUN)
        self.assertIn("shellCursor++ % (ExtraShellSlots + 1)", GUN)
        self.assertIn("shellTrail[i] = new ShellState", GUN)
        self.assertIn("for (int i = 0; i < shellTrail.Length; i++)", GUN)
        self.assertIn("slot.velocity += Physics.gravity * dt;", GUN)
        self.assertIn("if (slot.velocity.y < 0.5f) slot.rested = true;", GUN)
        eject = GUN.split("private void EjectCasingNow()",1)[1].split(
            "private void HideCasing()",1)[0]
        self.assertNotIn("GameObject.CreatePrimitive",eject)
        self.assertNotIn("new GameObject(",eject)
        # At the fastest authored rate ten slots live for more than
        # one ballistic fall from the muzzle to foot level.
        fastest_rpm = max(gun["rpm"] for gun in CAT["weapons"])
        cycle = (9 + 1) * 60.0 / fastest_rpm
        fall = (1.65 + math.sqrt(1.65**2 + 2 * 9.81 * 1.3)) / 9.81
        self.assertGreater(cycle,fall)

    def test_nearby_rival_only_pooled_visuals(self):
        self.assertIn("private const int MaximumSlots = 6;",FX)
        self.assertIn("if (observer == null || shooter == null || Time.timeScale <= 0f ||",FX)
        self.assertIn("70f * 70f",FX)
        self.assertIn("slots[index] = slot = CreateSlot();",FX)
        self.assertIn("slot.tracer.widthMultiplier = .008f;",FX)
        self.assertIn("casing.pose.Rotate(casing.spin * dt, Space.World);",FX)
        self.assertIn("owner.ShowRivalGunfire(transform, shotHit.point, carriedGun);",RIVAL)
        self.assertIn("shotHit.point, carriedGun.headshotMultiplier);",RIVAL)
        self.assertIn("AarisEquippedGun.SharedMuzzleFlameMesh()",FX)
        self.assertNotIn("ShowRivalGunfire",RIVAL.split("private void TickVirtualCombat",1)[1])

    def test_rival_casing_survives_tracer_slot_reuse(self):
        self.assertIn("private const int MaximumSlots = 6;", FX)
        self.assertIn("private const int MaximumCasingSlots = 12;", FX)
        self.assertIn("new CasingSlot[MaximumCasingSlots]", FX)
        emit = FX.split("internal void Emit(Transform shooter", 1)[1].split(
            "private void Update()", 1)[0]
        self.assertIn("nextSlot++ % MaximumSlots", emit)
        self.assertIn("nextCasingSlot++ % MaximumCasingSlots", emit)
        self.assertIn("casings[casingIndex] = casing = CreateCasingSlot();", emit)
        self.assertIn("casing.renderer.enabled = true;", emit)
        self.assertIn("casing.until = now + .85f;", emit)
        self.assertNotIn("slot.casing", emit)
        self.assertNotIn("GameObject.CreatePrimitive", emit)
        self.assertNotIn("new GameObject(", emit)
        updates = FX.split("private void Update()", 1)[1]
        self.assertIn("for (int i = 0; i < slots.Length; i++)", updates)
        self.assertIn("for (int i = 0; i < casings.Length; i++)", updates)
        self.assertIn("if (casing == null || !casing.renderer.enabled) continue;", updates)
        self.assertIn("casing.velocity += Physics.gravity * dt;", updates)
        self.assertIn("if (casing.velocity.y < .5f) casing.grounded = true;", updates)
        self.assertNotIn("Physics.Raycast", updates)
        self.assertIn("Destroy(hitbox);", FX)
        self.assertNotIn("AddComponent<Rigidbody>", FX.split("private CasingSlot CreateCasingSlot()",1)[1])

    def test_bot_death_spawns_crate_before_destruction(self):
        death=RIVAL.split("private void OnDied(AarisOfflineHealth defeated)",1)[1].split(
            "private void OnDestroy()",1)[0]
        for item in ("if (capsule != null) capsule.enabled = false;",
                     "owner.DropRivalLoot(transform.position, carriedGun,",
                     "carriedMagazine, carriedReserve, medkits, remoteLootSeed);",
                     "owner.RivalDefeated(this);","Destroy(gameObject, 1.2f);"):
            self.assertIn(item,death)
        self.assertLess(death.index("owner.DropRivalLoot("),
                        death.index("Destroy(gameObject, 1.2f);"))
        self.assertIn("private const int MaxVisuals = 16;",CRATE)
        self.assertIn("new List<Drop>(100);",CRATE)
        self.assertIn("Time.time + 0.25f;",CRATE)
        self.assertIn("unusedVisuals.Pop() : CreateVisual();",CRATE)
        self.assertIn("Vertical loot marker",CRATE)
        self.assertIn("ground.point.y",CRATE)

    def test_crate_hint_matches_actual_pickup_priority_and_range(self):
        self.assertIn("out string weapon, out float distance, out bool canPick", CRATE)
        self.assertIn("instance.Nearest(seeker.position, PickupRadius, true)", CRATE)
        self.assertIn("canPick = inReach != null;", CRATE)
        self.assertIn("Drop drop = inReach ?? instance.Nearest", CRATE)
        self.assertIn("cratePickupReady || closest == null", HUD)
        self.assertIn('(cratePickupReady ? "  PICK" : "")', HUD)
        self.assertIn("instance.cachedPickupNear = false;", CRATE)

    def test_visual_budget_keeps_closest_crates_when_saturated(self):
        world = CRATE.split("private void Update()", 1)[1].split(
            "private void Release(Drop drop)", 1)[0]
        self.assertIn("Drop furthestVisible = null;", world)
        self.assertIn("if (liveVisuals >= MaxVisuals)", world)
        self.assertIn("Release(furthestVisible);", world)
        self.assertIn("bestDistance + 0.25f >= furthestVisualDistance", world)
        self.assertLess(world.index("Release(furthestVisible);"),
                        world.index("CreateVisual();"))
        self.assertIn("nextPoll = Time.time + 0.25f;", world)

    def test_full_slot_exchange_reserves_drop_and_rolls_back_on_failure(self):
        exchange = INVENTORY.split("internal bool TryPickupDeathCrateGun(",1)[1].split(
            "internal int AddDeathCrateAmmo",1)[0]
        self.assertIn("if (RequiresExchangeDrop)", exchange)
        self.assertIn("lootWorld.TryExchangeDeathCrateGun(incoming)", exchange)
        self.assertIn("displaced == null;", exchange)
        method = read("AarisWeaponLootWorld.cs").split(
            "internal bool TryExchangeDeathCrateGun(",1)[1].split(
            "public bool TryDropEquipped()",1)[0]
        self.assertIn("!HasLandingAuthority", method)
        self.assertIn("player != inventory.transform", method)
        self.assertIn("!TryFindDropSpot(out Vector3 point, requireReachable: true)",method)
        self.assertIn("deferVisual: true", method)
        self.assertLess(method.index("CreateDropped(outgoing, point"),
                        method.index("inventory.TryPickup(incoming,"))
        self.assertIn("UndoDrop(reserved);", method)
        self.assertIn("if (reserved == null) return false;", method)
        self.assertIn("requestPickupUntil = 0f;", method)

    def test_ammo_remainder_goes_to_matching_stowed_weapon(self):
        take = INVENTORY.split("internal int AddDeathCrateAmmo(",1)[1].split(
            "public bool DropCurrent()",1)[0]
        self.assertIn("added += equipped.AddReserve(amount);",take)
        self.assertIn("added += stowed.AddReserve(amount - added);",take)
        self.assertIn("return added;",take)
        self.assertLess(take.index("equipped.AddReserve(amount)"),
                        take.index("stowed.AddReserve(amount - added)"))

    def test_spent_medkits_do_not_respawn_in_death_crates(self):
        spawn = CRATE.split("internal void Spawn(",1)[1].split(
            "internal static bool HasPickupNear",1)[0]
        self.assertIn("if (state == null && medkits <= 0) return;",spawn)
        self.assertIn("medkits = Mathf.Max(0, medkits)",spawn)
        self.assertNotIn("Mathf.Max(1, medkits)",spawn)

    def test_atomic_pickup_cannot_destroy_full_inventory_weapon(self):
        take=CRATE.split("internal static bool TryLootNearest(",1)[1].split(
            "// Check line of sight only",1)[0]
        self.assertIn("inventory.TryPickupDeathCrateGun(transfer)",take)
        self.assertIn("drop.gun = null;",take)
        self.assertLess(take.index("inventory.TryPickupDeathCrateGun(transfer)"),
                        take.index("drop.gun = null;"))
        self.assertIn("inventory.AddDeathCrateAmmo",take)
        self.assertIn("medical.AddDeathCrateMedkits",take)
        self.assertIn("drop.looseAmmo -= added;",take)
        self.assertIn("drop.medkits -= added;",take)
        self.assertIn("mustReach && !Reachable(point, drop.position)",CRATE)
        self.assertIn("if (AarisDeathCrateWorld.TryLootNearest(transform, this)) return;",INVENTORY)
        self.assertIn("if (AcceptsPlayerWeaponInput && lootWorld != null)",INVENTORY)
        self.assertIn("internal int AddDeathCrateMedkits(int amount)",MEDICAL)
        self.assertIn("maxMedkits - medkits",MEDICAL)
        self.assertIn("AarisDeathCrateWorld.HasPickupNear(transform)",INVENTORY)
        self.assertIn("AarisDeathCrateWorld.TryGetNearbyHint",HUD)

if __name__ == "__main__":
    unittest.main()

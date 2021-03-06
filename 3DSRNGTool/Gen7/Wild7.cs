﻿using System.Linq;
using PKHeX.Core;
using Pk3DSRNGTool.Core;

namespace Pk3DSRNGTool
{
    public class Wild7 : WildRNG
    {
        private static ulong getrand => RNGPool.getrand64;
        private static uint getsosrand => SOSRNG.getrand;
        private static void time_elapse(int n) => RNGPool.time_elapse7(n);
        private static void Advance(int n) => RNGPool.Advance(n);

        public byte SpecialEnctr; // !=0 means there is ub/qr present
        public byte Levelmin, Levelmax;
        public byte SpecialLevel;
        public bool CompoundEye;
        public bool UB;
        public bool Fishing;
        public bool SOS;
        public bool Crabrawler;

        public byte DelayType;
        public int DelayTime;

        private bool IsSpecial;
        private bool IsMinior => SpecForm[slot] == 774;
        private bool IsUB => UB && IsSpecial;
        private bool IsShinyLocked => IsUB && SpecForm[0] <= 800; // Not USUM UB
        private bool NormalSlot => !IsSpecial || Fishing;

        protected override int PIDroll_count => (ShinyCharm && !IsShinyLocked ? 3 : 1) + (SOS ? SOSRNG.PIDBonus : 0);

        // UM v1.2 sub_3A7FE8
        protected override void CheckLeadAbility(ulong rand100)
        {
            SynchroPass = rand100 >= 50;
            CuteCharmPass = CuteCharmGender > 0 && rand100 < 67;
            StaticMagnetPass = StaticMagnet && rand100 >= 50;
            LevelModifierPass = ModifiedLevel != 0 && rand100 >= 50;
        }

        public override void Delay() => RNGPool.WildDelay7();
        private void InlineDelay()
        {
            switch (DelayType)
            {
                case 1:
                    time_elapse(DelayTime - 2);
                    RNGPool.modelnumber--;
                    time_elapse(2);
                    break;
                default:
                    time_elapse(DelayTime);
                    break;
            }
        }
        public override RNGResult Generate()
        {
            ResultW7 rt = new ResultW7();
            rt.Level = SpecialLevel;

            if (Fishing)
            {
                IsSpecial = rt.IsSpecial = getrand % 100 >= SpecialEnctr;
                time_elapse(12);
                if (IsSpecial) // Predict hooked item
                {
                    int mark = RNGPool.index;
                    time_elapse(34);
                    rt.SpecialVal = (byte)(getrand % 100);
                    RNGPool.Rewind(mark); // Don't need to put modelstatus back
                }
            }
            else if (SpecialEnctr > 0)
                IsSpecial = rt.IsSpecial = getrand % 100 < SpecialEnctr;

            if (SOS)
            {
                SOSRNG.Reset();
                SOSRNG.Advance(2); // Call Rate Check
                CheckLeadAbility(getsosrand % 100);
                if (SOSRNG.Weather && (rt.Slot = slot = SOSRNG.getWeatherSlot(getsosrand % 100)) > 7) // No Electric/Steel Type in weather sos slots
                    rt.IsSpecial = true;
                else
                    rt.Slot = StaticMagnetPass ? getsmslot(getsosrand) : getslot((int)(getsosrand % 100));
                rt.Level = (byte)(getsosrand % (uint)(Levelmax - Levelmin + 1) + Levelmin);
                FluteBoost = getFluteBoost(getsosrand % 100);
                ModifyLevel(rt);
            }
            else if (Crabrawler)
            {
                CheckLeadAbility(getrand % 100);
                rt.Slot = slot = 1;
                rt.Level = ModifiedLevel;
            }
            else if (NormalSlot) // Normal wild
            {
                CheckLeadAbility(getrand % 100);
                rt.Slot = StaticMagnetPass ? getsmslot(getrand) : getslot((int)(getrand % 100));
                rt.Level = (byte)(getrand % (ulong)(Levelmax - Levelmin + 1) + Levelmin);
                FluteBoost = getFluteBoost(getrand % 100);
                ModifyLevel(rt);
                if (IsMinior) rt.Forme = (byte)(getrand % 7);
            }
            else // UB or QR
            {
                slot = 0;
                time_elapse(7);
                CheckLeadAbility(getrand % 100);
                time_elapse(3);
            }
            rt.Species = (short)(SpecForm[slot] & 0x7FF);

            if (DelayTime > 0)
                InlineDelay();

            if (!SOS)
                Advance(60);

            //Encryption Constant
            rt.EC = (uint)getrand;

            //PID
            for (int i = PIDroll_count; i > 0; i--)
            {
                rt.PID = (uint)getrand;
                if (rt.PSV == TSV)
                {
                    if (IsShinyLocked)
                        rt.PID ^= 0x10000000;
                    else
                    {
                        rt.Shiny = true;
                        rt.SquareShiny = rt.PRV == TRV;
                    }
                    break;
                }
            }

            //IV
            rt.IVs = new int[6];
            for (int i = PerfectIVCount; i > 0;)
            {
                int tmp = (int)(getrand % 6);
                if (rt.IVs[tmp] == 0)
                {
                    i--; rt.IVs[tmp] = 31;
                }
            }
            for (int i = 0; i < 6; i++)
                if (rt.IVs[i] == 0)
                    rt.IVs[i] = (int)(getrand & 0x1F);

            //Ability
            rt.Ability = (byte)(IsUB ? 1 : (getrand & 1) + 1);

            //Nature
            rt.Nature = (rt.Synchronize = SynchroPass) && Synchro_Stat < 25 ? Synchro_Stat : (byte)(getrand % 25);

            //Gender
            rt.Gender = RandomGender[slot] ? (CuteCharmPass ? CuteCharmGender : (byte)(getrand % 252 >= Gender[slot] ? 1 : 2)) : Gender[slot];

            //Item
            rt.Item = getHeldItem(SOS ? getsosrand % 100 : NormalSlot ? getrand % 100 : 100, CompoundEye);

            if (Fishing && rt.IsSpecial)
                rt.Slot = getHookedItemSlot(rt.SpecialVal); //Fishing item slots
            else if (SOS)
                SOSRNG.PostGeneration(rt); // IVs and HA

            return rt;
        }

        public override void Markslots()
        {
            IV3 = new bool[SpecForm.Length];
            RandomGender = new bool[SpecForm.Length];
            Gender = new byte[SpecForm.Length];
            var smslot = new int[0].ToList();
            for (int i = 0; i < SpecForm.Length; i++)
            {
                if (SpecForm[i] == 0) continue;
                PersonalInfo info = PersonalTable.USUM.getFormeEntry(SpecForm[i] & 0x7FF, SpecForm[i] >> 11);
                byte genderratio = (byte)info.Gender;
                IV3[i] = info.EggGroups[0] == 0xF && !Pokemon.BabyMons.Contains(SpecForm[i] & 0x7FF);
                Gender[i] = FuncUtil.getGenderRatio(genderratio);
                RandomGender[i] = FuncUtil.IsRandomGender(genderratio);
                if (Static && info.Types.Contains(Pokemon.electric) || Magnet && info.Types.Contains(Pokemon.steel)) // Collect slots
                    smslot.Add(i);
            }
            StaticMagnetSlot = smslot.Select(s => (byte)s).ToArray();
            if (0 == (NStaticMagnetSlot = (ulong)smslot.Count))
                Static = Magnet = false;
            if (ModifiedLevel == 101)
                ModifiedLevel = Levelmax;
            if (UB) IV3[0] = true; // For UB Template
        }

        private byte getsmslot(ulong rand) => slot = StaticMagnetSlot[rand % NStaticMagnetSlot];

        public static byte getHeldItem(ulong rand, bool CompoundEye = false)
        {
            if (rand < (CompoundEye ? 60u : 50))
                return 0; // 50%
            if (rand < (CompoundEye ? 80u : 55))
                return 1; // 5%
            return 3; // None
        }

        public static byte getSpecialRate(int category)
        {
            switch (category)
            {
                case 1: return 80;
                case 2: return 50;
                case 3: return 80;
                default: return 0;
            }
        }

        public byte[] HookedItemSlot;
        public byte getHookedItemSlot(byte? rand)
        {
            if (rand == null)
                return 0;
            if (rand < HookedItemSlot[0])
                return 1;
            if (rand < HookedItemSlot[1])
                return 2;
            return 3;
        }
    }

    public struct FishingSetting
    {
        public int basedelay;
        public bool suctioncups;
        public int platdelay;
        public int pkmdelay;
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace PKHeX.Core
{
    public abstract class GBPKM : PKM
    {
        internal const int STRLEN_J = 6;
        internal const int STRLEN_U = 11;

        public sealed override int MaxBallID => -1;
        public sealed override int MaxGameID => -1;
        public sealed override int MaxIV => 15;
        public sealed override int MaxEV => ushort.MaxValue;
        public sealed override int OTLength => Japanese ? 5 : 7;
        public sealed override int NickLength => Japanese ? 5 : 10;

        public sealed override IReadOnlyList<ushort> ExtraBytes => Array.Empty<ushort>();

        public sealed override string FileNameWithoutExtension
        {
            get
            {
                string form = AltForm > 0 ? $"-{AltForm:00}" : string.Empty;
                string star = IsShiny ? " ★" : string.Empty;
                return $"{Species:000}{form}{star} - {Nickname} - {Checksums.CRC16_CCITT(Encrypt()):X4}";
            }
        }

        private int StringLength => Japanese ? STRLEN_J : STRLEN_U;
        public sealed override bool Japanese => otname.Length == STRLEN_J;
        public sealed override byte[] Data { get; }

        protected GBPKM(byte[] data, bool jp = false)
        {
            int partySize = SIZE_PARTY;
            if (data.Length != partySize)
                Array.Resize(ref data, partySize);
            Data = data;
            int strLen = jp ? STRLEN_J : STRLEN_U;

            // initialize string buffers
            otname = new byte[strLen];
            nick = new byte[strLen];
            for (int i = 0; i < otname.Length; i++)
                otname[i] = nick[i] = 0x50;
        }

        internal byte[] otname;
        internal byte[] nick;

        // Trash Bytes
        public sealed override byte[] Nickname_Trash { get => nick; set { if (value.Length == nick.Length) nick = value; } }
        public sealed override byte[] OT_Trash { get => otname; set { if (value.Length == otname.Length) otname = value; } }

        public sealed override byte[] EncryptedPartyData => Encrypt();
        public sealed override byte[] EncryptedBoxData => Encrypt();
        public sealed override byte[] DecryptedBoxData => Encrypt();
        public sealed override byte[] DecryptedPartyData => Encrypt();

        private bool? _isnicknamed;

        public sealed override bool IsNicknamed
        {
            get => _isnicknamed ??= !nick.SequenceEqual(GetNonNickname(GuessedLanguage()));
            set
            {
                _isnicknamed = value;
                if (_isnicknamed == false)
                    SetNotNicknamed(GuessedLanguage());
            }
        }

        protected bool IsNicknamedBank
        {
            get
            {
                var spName = SpeciesName.GetSpeciesNameGeneration(Species, GuessedLanguage(), Format);
                return Nickname != spName;
            }
        }

        public sealed override int Language
        {
            get
            {
                if (Japanese)
                    return (int)LanguageID.Japanese;
                if (Korean)
                    return (int)LanguageID.Korean;
                if (StringConverter12.IsG12German(otname))
                    return (int)LanguageID.German; // german
                int lang = SpeciesName.GetSpeciesNameLanguage(Species, Nickname, Format);
                if (lang > 0)
                    return lang;
                return 0;
            }
            set
            {
                if (Japanese)
                    return;
                if (Korean)
                    return;

                if (IsNicknamed)
                    return;
                SetNotNicknamed(value);
            }
        }

        public sealed override string Nickname
        {
            get
            {
                if (Korean)
                    return StringConverter2KOR.GetString2KOR(nick, 0, nick.Length);
                return StringConverter12.GetString1(nick, 0, nick.Length, Japanese);
            }
            set
            {
                if (!IsNicknamed && Nickname == value)
                    return;

                GetStringSpecial(value, StringLength).CopyTo(nick, 0);
            }
        }

        public sealed override string OT_Name
        {
            get
            {
                if (Korean)
                    return StringConverter2KOR.GetString2KOR(otname, 0, otname.Length);
                return StringConverter12.GetString1(otname, 0, otname.Length, Japanese);
            }
            set => GetStringSpecial(value, StringLength).CopyTo(otname, 0);
        }

        public sealed override int Gender
        {
            get
            {
                int gv = PersonalInfo.Gender;
                if (gv == 255)
                    return 2;
                if (gv == 254)
                    return 1;
                if (gv == 0)
                    return 0;
                return IV_ATK > gv >> 4 ? 0 : 1;
            }
            set { }
        }

        #region Future, Unused Attributes
        public sealed override bool IsGenderValid() => true; // not a separate property, derived via IVs
        public sealed override uint EncryptionConstant { get => 0; set { } }
        public sealed override uint PID { get => 0; set { } }
        public sealed override int Nature { get => 0; set { } }
        public sealed override ushort Sanity { get => 0; set { } }
        public sealed override bool ChecksumValid => true;
        public sealed override ushort Checksum { get => 0; set { } }
        public sealed override bool FatefulEncounter { get => false; set { } }
        public sealed override int TSV => 0x0000;
        public sealed override int PSV => 0xFFFF;
        public sealed override int Characteristic => -1;
        public sealed override int MarkValue { get => 0; protected set { } }
        public sealed override int Ability { get => -1; set { } }
        public sealed override int CurrentHandler { get => 0; set { } }
        public sealed override int Egg_Location { get => 0; set { } }
        public sealed override int Ball { get => 0; set { } }
        public sealed override int SID { get => 0; set { } }
        #endregion

        public sealed override bool IsShiny => IV_DEF == 10 && IV_SPE == 10 && IV_SPC == 10 && (IV_ATK & 2) == 2;
        private int HPBitValPower => ((IV_ATK & 8) >> 0) | ((IV_DEF & 8) >> 1) | ((IV_SPE & 8) >> 2) | ((IV_SPC & 8) >> 3);
        public sealed override int HPPower => (((5 * HPBitValPower) + (IV_SPC & 3)) >> 1) + 31;

        public sealed override int HPType
        {
            get => ((IV_ATK & 3) << 2) | (IV_DEF & 3);
            set
            {
                IV_DEF = ((IV_DEF >> 2) << 2) | (value & 3);
                IV_DEF = ((IV_ATK >> 2) << 2) | ((value >> 2) & 3);
            }
        }

        public sealed override int AltForm
        {
            get
            {
                if (Species != 201) // Unown
                    return 0;

                uint formeVal = 0;
                formeVal |= (uint)((IV_ATK & 0x6) << 5);
                formeVal |= (uint)((IV_DEF & 0x6) << 3);
                formeVal |= (uint)((IV_SPE & 0x6) << 1);
                formeVal |= (uint)((IV_SPC & 0x6) >> 1);
                return (int)(formeVal / 10);
            }
            set { }
        }

        public abstract int EV_SPC { get; set; }
        public sealed override int EV_SPA { get => EV_SPC; set => EV_SPC = value; }
        public sealed override int EV_SPD { get => EV_SPC; set { } }
        public abstract ushort DV16 { get; set; }
        public sealed override int IV_HP { get => ((IV_ATK & 1) << 3) | ((IV_DEF & 1) << 2) | ((IV_SPE & 1) << 1) | ((IV_SPC & 1) << 0); set { } }
        public sealed override int IV_ATK { get => (DV16 >> 12) & 0xF; set => DV16 = (ushort)((DV16 & ~(0xF << 12)) | (ushort)((value > 0xF ? 0xF : value) << 12)); }
        public sealed override int IV_DEF { get => (DV16 >> 8) & 0xF; set => DV16 = (ushort)((DV16 & ~(0xF << 8)) | (ushort)((value > 0xF ? 0xF : value) << 8)); }
        public sealed override int IV_SPE { get => (DV16 >> 4) & 0xF; set => DV16 = (ushort)((DV16 & ~(0xF << 4)) | (ushort)((value > 0xF ? 0xF : value) << 4)); }
        public int IV_SPC { get => (DV16 >> 0) & 0xF; set => DV16 = (ushort)((DV16 & ~(0xF << 0)) | (ushort)((value > 0xF ? 0xF : value) << 0)); }
        public sealed override int IV_SPA { get => IV_SPC; set => IV_SPC = value; }
        public sealed override int IV_SPD { get => IV_SPC; set { } }

        public void SetNotNicknamed() => nick = GetNonNickname(GuessedLanguage()).ToArray();
        public void SetNotNicknamed(int language) => nick = GetNonNickname(language).ToArray();

        private IEnumerable<byte> GetNonNickname(int language)
        {
            var name = SpeciesName.GetSpeciesNameGeneration(Species, language, Format);
            var bytes = SetString(name, StringLength);
            var data = bytes.Concat(Enumerable.Repeat((byte)0x50, nick.Length - bytes.Length));
            if (!Korean)
                data = data.Select(b => (byte)(b == 0xF2 ? 0xE8 : b)); // Decimal point<->period fix
            return data;
        }

        public int GuessedLanguage(int fallback = (int)LanguageID.English)
        {
            int lang = Language;
            if (lang > 0)
                return lang;
            if (fallback == (int)LanguageID.French || fallback == (int)LanguageID.German) // only other permitted besides English
                return fallback;
            return (int)LanguageID.English;
        }

        /// <summary>
        /// Tries to guess the source language ID when transferred to future generations (7+)
        /// </summary>
        /// <param name="destLanguage">Destination language ID</param>
        /// <returns>Source language ID</returns>
        protected int TransferLanguage(int destLanguage)
        {
            // if the Species name of the destination language matches the current nickname, transfer with that language.
            var expect = SpeciesName.GetSpeciesNameGeneration(Species, destLanguage, 2);
            if (Nickname == expect)
                return destLanguage;
            return GuessedLanguage(destLanguage);
        }

        public sealed override ushort[] GetStats(PersonalInfo p)
        {
            var lv = Stat_Level;
            ushort[] stats =
            {
                GetStat(p.HP , IV_HP , EV_HP , lv),
                GetStat(p.ATK, IV_ATK, EV_ATK, lv),
                GetStat(p.DEF, IV_DEF, EV_DEF, lv),
                GetStat(p.SPE, IV_SPE, EV_SPE, lv),
                GetStat(p.SPA, IV_SPA, EV_SPA, lv),
                GetStat(p.SPD, IV_SPD, EV_SPD, lv),
            };
            stats[0] += (ushort)(5 + lv); // HP
            return stats;
        }

        protected static ushort GetStat(int BV, int IV, int EV, int LV)
        {
            EV = (ushort)Math.Min(255, Math.Sqrt(EV) + 1) >> 2;
            return (ushort)((((2 * (BV + IV)) + EV) * LV / 100) + 5);
        }

        public sealed override int GetMovePP(int move, int ppUpCount)
        {
            var pp = base.GetMovePP(move, 0);
            return pp + (ppUpCount * Math.Min(7, pp / 5));
        }

        /// <summary>
        /// Applies <see cref="PKM.IVs"/> to the <see cref="PKM"/> to make it shiny.
        /// </summary>
        public sealed override void SetShiny()
        {
            IV_ATK |= 2;
            IV_DEF = 10;
            IV_SPE = 10;
            IV_SPA = 10;
        }

        protected string GetString(int Offset, int Count)
        {
            if (Korean)
                return StringConverter2KOR.GetString2KOR(Data, Offset, Count);
            return StringConverter12.GetString1(Data, Offset, Count, Japanese);
        }

        private byte[] SetString(string value, int maxLength)
        {
            if (Korean)
                return StringConverter2KOR.SetString2KOR(value, maxLength - 1);
            return StringConverter12.SetString1(value, maxLength - 1, Japanese);
        }

        private byte[] GetStringSpecial(string value, int length)
        {
            byte[] strdata = SetString(value, length);
            if (nick.All(b => b != 0) || nick[length - 1] != 0x50 || Array.FindIndex(nick, b => b == 0) != strdata.Length - 1)
                return strdata;

            int firstInd = Array.FindIndex(nick, b => b == 0);
            for (int i = firstInd; i < length - 1; i++)
            {
                if (nick[i] != 0)
                    break;
            }

            return strdata.Take(strdata.Length - 1).ToArray();
        }
    }
}

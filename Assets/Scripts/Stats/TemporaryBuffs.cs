using System;
using System.Collections.Generic;

namespace TheLastKnight.Stats
{
    public enum TemporaryBuff { IronWill, Might, Swiftness, Endurance, Purity, Regeneration, Fortitude, Undying, CriticalDamage }

    public readonly struct BuffSnapshot
    {
        public TemporaryBuff Kind { get; }
        public float Remaining { get; }
        public float Duration { get; }
        internal BuffSnapshot(TemporaryBuff kind, float remaining, float duration)
        { Kind = kind; Remaining = remaining; Duration = duration; }
    }

    // Timed effects have one lifecycle and one observable state for gameplay and
    // display. The clock seam uses Unity time in play and manual time in tests.
    public sealed class TemporaryBuffs
    {
        public const float PotionDuration = 90f;
        public const float IronWillDuration = 15f;
        public const float MightAttackBonus = 0.25f;
        public const float IronWillAttackBonus = 0.22f;
        private readonly Func<float> _now;
        private readonly float[] _expires = new float[9];
        private readonly bool[] _applied = new bool[9];

        public TemporaryBuffs(Func<float> now) => _now = now ?? throw new ArgumentNullException(nameof(now));
        private static int Index(TemporaryBuff kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= 9) throw new ArgumentOutOfRangeException(nameof(kind));
            return index;
        }
        public static float Duration(TemporaryBuff kind) => Index(kind) == 0 ? IronWillDuration : PotionDuration;
        public void Apply(TemporaryBuff kind)
        {
            int index = Index(kind);
            _expires[index] = _now() + Duration(kind);
            _applied[index] = true;
        }
        public void Remove(TemporaryBuff kind) => _applied[Index(kind)] = false;
        public bool IsActive(TemporaryBuff kind)
        {
            int index = Index(kind);
            return _applied[index] && _now() < _expires[index];
        }
        public float Remaining(TemporaryBuff kind)
        {
            int index = Index(kind);
            return _applied[index] ? Math.Max(0f, _expires[index] - _now()) : 0f;
        }

        // Returns one invalidation after expiry; display reads do not consume it.
        public bool Tick()
        {
            float now = _now();
            bool changed = false;
            for (int i = 0; i < _applied.Length; i++)
                if (_applied[i] && now >= _expires[i]) { _applied[i] = false; changed = true; }
            return changed;
        }

        public void GetActive(List<BuffSnapshot> target)
        {
            if (target == null) return;
            target.Clear();
            float now = _now();
            for (int i = 0; i < _applied.Length; i++)
                if (_applied[i] && now < _expires[i])
                    target.Add(new BuffSnapshot((TemporaryBuff)i, _expires[i] - now, Duration((TemporaryBuff)i)));
        }

        public float AttackBonus => (IsActive(TemporaryBuff.Might) ? MightAttackBonus : 0f) +
            (IsActive(TemporaryBuff.IronWill) ? IronWillAttackBonus : 0f);
        public float SpeedMultiplier => IsActive(TemporaryBuff.Swiftness) ? 1.25f : 1f;
        public float FortitudeMultiplier => IsActive(TemporaryBuff.Fortitude) ? 1.25f : 1f;
        public float StaminaCostMultiplier => IsActive(TemporaryBuff.Endurance) ? 0.5f : 1f;
        public float DexCriticalMultiplier => IsActive(TemporaryBuff.CriticalDamage) ? 3f : 1.5f;
        public float ParryCriticalMultiplier => IsActive(TemporaryBuff.CriticalDamage) ? 5f : 2f;
    }
}

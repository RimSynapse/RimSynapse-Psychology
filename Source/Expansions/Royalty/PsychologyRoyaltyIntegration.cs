using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace RimSynapse.Expansions.Royalty
{
    public static class PsychologyRoyaltyIntegration
    {
        public static string GetSeniorTitle(Pawn pawn)
        {
            if (!ModsConfig.RoyaltyActive) return null;
            return GetSeniorTitleInternal(pawn);
        }

        public static bool IsNoble(Pawn pawn)
        {
            if (!ModsConfig.RoyaltyActive) return false;
            return IsNobleInternal(pawn);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetSeniorTitleInternal(Pawn pawn)
        {
            return pawn.royalty?.MostSeniorTitle?.def?.label;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsNobleInternal(Pawn pawn)
        {
            return pawn.royalty != null && pawn.royalty.AllTitlesForReading.Any();
        }
    }
}

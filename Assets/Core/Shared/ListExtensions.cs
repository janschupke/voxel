using System.Collections.Generic;

namespace Voxel
{
    /// <summary>Extension methods for collections.</summary>
    public static class ListExtensions
    {
        /// <summary>Shuffles the list in-place using Fisher-Yates.</summary>
        public static void Shuffle<T>(this List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

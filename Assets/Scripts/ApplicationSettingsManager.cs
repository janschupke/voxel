using UnityEngine;

namespace Voxel
{
    public class ApplicationSettingsManager : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
        }
    }
}

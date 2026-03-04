using UnityEngine;

namespace Voxel
{
    public static class GameSettings
    {
        private const string KeyCameraPosX = "CameraPosX";
        private const string KeyCameraPosZ = "CameraPosZ";
        private const string KeyCameraBlocksVisible = "CameraBlocksVisible";
        private const string KeyCameraSaved = "CameraSaved";

        public static void SaveCamera(float posX, float posZ, float blocksVisible)
        {
            PlayerPrefs.SetFloat(KeyCameraPosX, posX);
            PlayerPrefs.SetFloat(KeyCameraPosZ, posZ);
            PlayerPrefs.SetFloat(KeyCameraBlocksVisible, blocksVisible);
            PlayerPrefs.SetInt(KeyCameraSaved, 1);
            PlayerPrefs.Save();
        }

        public static bool TryLoadCamera(out float posX, out float posZ, out float blocksVisible)
        {
            posX = posZ = blocksVisible = 0f;
            if (PlayerPrefs.GetInt(KeyCameraSaved, 0) != 1) return false;

            posX = PlayerPrefs.GetFloat(KeyCameraPosX, 0f);
            posZ = PlayerPrefs.GetFloat(KeyCameraPosZ, 0f);
            blocksVisible = PlayerPrefs.GetFloat(KeyCameraBlocksVisible, 25f);
            return true;
        }
    }
}

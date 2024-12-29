using UnityEngine;
using UnityEngine.SceneManagement;

namespace Main_Menu_2
{
    public class Main_Menu : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void PlayGame()
        {
            SceneManager.LoadSceneAsync(2);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        public void ExitGame()
        {
            SceneManager.LoadSceneAsync(0);
        }
    }
}

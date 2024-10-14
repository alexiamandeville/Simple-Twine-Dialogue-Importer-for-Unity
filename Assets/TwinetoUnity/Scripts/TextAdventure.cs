using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

namespace SimpleTwineDialogue
{
    public class TextAdventure : MonoBehaviour
    {
        [Header("UI Components")]
        public TextMeshProUGUI passageText;
        public Button choiceButtonPrefab;
        public Transform choiceButtonContainer;
        public Transform imageContainer;
        public Image imagePrefab;

        int myChoices = 0;
        public TextMeshProUGUI myChoiceCounterUI;

        [Header("File Loading")]
        public bool loadFromWeb;

        [Header("Load from Web")]
        public string webFileURL;
        public string imageBaseURL;
        [Header("Load from Local")]
        public string localFileName;

        private TweeParser tweeParser;
        private Dictionary<string, TweeParser.Passage> passages;
        private string currentPassageTitle;

        void Start()
        {
            tweeParser = new TweeParser();
            if(loadFromWeb){
                StartCoroutine(LoadTweeFile(webFileURL));
                
            } else {
                StartCoroutine(LoadTweeFile(Path.Combine(Application.streamingAssetsPath, localFileName)));
            }
        }
       
        // When a choice is selected from the buttons, do something. Here, I'm just incrementing a choice counter.
        void OnChoiceSelected(string choiceTitle, string currentPassageText)
        {
            DisplayPassage(choiceTitle);
            myChoices += 1;
            myChoiceCounterUI.text = "Choices made: " + myChoices.ToString();
        }

        // Load the Twee file. I'm loading a Twee file from an AWS server here, but it could be hosted on any webserver. 
        // I did this in order to make it easier to build for web and mobile. 
        // If this is for PC/Mac, you can load a Twee file from the streaming assets folder.
        IEnumerator LoadTweeFile(string filePath)
        {
            // Check if this uses local files or files loaded from web
            if (loadFromWeb)
            {
                // Continue with the web request to load files
                Debug.Log("Starting twee file download from: " + webFileURL);
                UnityWebRequest request = UnityWebRequest.Get(filePath);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError(request.error);
                }
                else
                {
                    string text = request.downloadHandler.text;
                    passages = tweeParser.ParseTweeFileFromText(text);
                    
                    CheckForStartPassage();
                }
            } else {
                // Continue loading locally
                if (File.Exists(filePath))
                {
                    string text = File.ReadAllText(filePath, Encoding.UTF8);
                    passages = tweeParser.ParseTweeFileFromText(text);
                    
                    CheckForStartPassage();

                    yield break; // Exit the coroutine since we're using local file loading
                }
                else
                {
                    Debug.LogError("Twee file not found in StreamingAssets: " + filePath);
                    yield break;
                }

            }

        }

        // Load the image using the filename from Twee file
        IEnumerator LoadImage(string imageFileName)
        {
            if (imagePrefab == null || imageContainer == null)
            {
                Debug.LogError("ImagePrefab or ImageContainer is not assigned.");
                yield break;
            }

            string imagePath;

            if (loadFromWeb)
            {
                // Load from web
                imagePath = Path.Combine(imageBaseURL, imageFileName);
                Debug.Log("Starting texture download from: " + imagePath);
                UnityWebRequest request = UnityWebRequestTexture.GetTexture(imagePath);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("Download error: " + request.error);
                }
                else
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    if (texture == null)
                    {
                        Debug.LogError("Failed to retrieve texture from web.");
                        yield break;
                    }

                    Debug.Log("Texture downloaded. Width: " + texture.width + ", Height: " + texture.height);

                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                    Image image = Instantiate(imagePrefab, imageContainer);
                    image.sprite = sprite;
                    image.gameObject.SetActive(true);
                }
            }
            else
            {
                // Load from local StreamingAssets
                imagePath = Path.Combine(Application.streamingAssetsPath, imageFileName);

                if (File.Exists(imagePath))
                {
                    Debug.Log("Loading texture from local file: " + imagePath);
                    byte[] imageBytes = File.ReadAllBytes(imagePath);
                    Texture2D texture = new Texture2D(2, 2); // Texture size will be updated when the image is loaded
                    texture.LoadImage(imageBytes);

                    if (texture == null)
                    {
                        Debug.LogError("Failed to load texture from StreamingAssets.");
                        yield break;
                    }

                    Debug.Log("Texture loaded from StreamingAssets. Width: " + texture.width + ", Height: " + texture.height);

                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                    Image image = Instantiate(imagePrefab, imageContainer);
                    image.sprite = sprite;
                    image.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogError("Image file not found in StreamingAssets: " + imagePath);
                }
            }
        }

        // Display the passage and it's contents onto the UI.
        public void DisplayPassage(string passageTitle)
        {
            if (!passages.TryGetValue(passageTitle, out var passage))
            {
                Debug.LogError("Passage not found: " + passageTitle);
                return;
            }
            ClearChoices();
            ClearImages();

            currentPassageTitle = passageTitle;
            passageText.text = passage.Body;

            foreach (var choice in passage.Choices)
            {
                var choiceText = choice.Substring(2, choice.IndexOf("]]") - 2);
                var choiceTitle = choiceText.Split('|')[1];

                var choiceButton = Instantiate(choiceButtonPrefab, choiceButtonContainer);

                // You can change this split if your Twee file has a different symbol in the choices.
                choiceButton.GetComponentInChildren<TextMeshProUGUI>().text = choiceText.Split('|')[0]; 
                var parts = choiceText.Split('|');

                choiceButton.onClick.AddListener(() => OnChoiceSelected(choiceTitle, passage.Body));
            }

            // Load images for the passage
            foreach (var imageFileName in passage.Images)
            {
                StartCoroutine(LoadImage(imageFileName));
            }
        }

        void CheckForStartPassage(){
                if (passages.ContainsKey("Start"))
                {
                    Debug.Log("Passage 'Start' found.");
                    DisplayPassage("Start");  // Assume "Start" is the title of the initial passage
                }
                else
                {
                    Debug.LogError("Passage 'Start' not found.");
                }
        }

        // Clear out the button choices to make room for the new ones.
        void ClearChoices()
        {
            foreach (Transform child in choiceButtonContainer)
            {
                Destroy(child.gameObject);
            }

        }

        // Clear out the image to make room for the new one.
        void ClearImages()
        {
            foreach (Transform child in imageContainer)
            {
                Destroy(child.gameObject);
            }
        }

    }
}

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
    /// <summary>
    /// TextAdventure - A Twine/Twee story player for Unity
    /// 
    /// This script loads and displays interactive fiction stories written in the Twee format.
    /// It handles passage navigation, choice buttons, and image loading from either web or local sources.
    /// 
    /// Key Features:
    /// - Parses Twee files with support for multiple link formats: [[Target]], [[Text|Target]], [[Text->Target]]
    /// - Loads files from web URLs or local StreamingAssets folder
    /// - Displays images referenced in passages
    /// - Creates interactive choice buttons for story navigation
    /// - Tracks user choices
    /// 
    /// Setup Requirements:
    /// 1. Assign UI components in the Inspector (passageText, choiceButtonPrefab, etc.)
    /// 2. Set loadFromWeb to true for web loading or false for local files
    /// 3. For web: Set webFileURL and imageBaseURL
    /// 4. For local: Place Twee file in StreamingAssets folder and set localFileName
    /// 5. Ensure your Twee file has a passage named "Start" (case-sensitive)
    /// </summary>
    public class TextAdventure : MonoBehaviour
    {
        [Header("UI Components")]
        // Text component to display passage content
        public TextMeshProUGUI passageText;
        
        // Prefab for choice buttons that will be instantiated
        public Button choiceButtonPrefab;
        
        // Container where choice buttons will be spawned
        public Transform choiceButtonContainer;
        
        // Container where images will be displayed
        public Transform imageContainer;
        
        // Prefab for images that will be instantiated
        public Image imagePrefab;

        // Counter for tracking how many choices the player has made
        int myChoices = 0;
        public TextMeshProUGUI myChoiceCounterUI;

        [Header("File Loading")]
        // Toggle between web and local file loading
        public bool loadFromWeb;

        [Header("Load from Web")]
        // URL to the Twee file hosted on a web server
        public string webFileURL;
        
        // Base URL where images are hosted (images will be loaded from imageBaseURL/imageName)
        public string imageBaseURL;
        
        [Header("Load from Local")]
        // Filename of the Twee file in the StreamingAssets folder
        public string localFileName;

        // Parser instance for reading Twee files
        private TweeParser tweeParser;
        
        // Dictionary storing all passages from the Twee file
        private Dictionary<string, TweeParser.Passage> passages;
        
        // Title of the currently displayed passage
        private string currentPassageTitle;

        /// <summary>
        /// Initialize the text adventure and start loading the Twee file
        /// </summary>
        void Start()
        {
            tweeParser = new TweeParser();
            if(loadFromWeb){
                StartCoroutine(LoadTweeFile(webFileURL));
                
            } else {
                StartCoroutine(LoadTweeFile(Path.Combine(Application.streamingAssetsPath, localFileName)));
            }
        }
       
        /// <summary>
        /// Called when a choice button is clicked
        /// </summary>
        /// <param name="choiceTitle">The target passage to navigate to</param>
        /// <param name="currentPassageText">The text of the current passage</param>
        void OnChoiceSelected(string choiceTitle, string currentPassageText)
        {
            DisplayPassage(choiceTitle);
            myChoices += 1;
            myChoiceCounterUI.text = "Choices made: " + myChoices.ToString();
        }

        /// <summary>
        /// Load and parse the Twee file from either web or local storage
        /// </summary>
        /// <param name="filePath">Path to the Twee file (URL for web, file path for local)</param>
        IEnumerator LoadTweeFile(string filePath)
        {
            // Check if this uses local files or files loaded from web
            if (loadFromWeb)
            {
                // Load from web using UnityWebRequest
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
                // Load from local StreamingAssets folder
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

        /// <summary>
        /// Load an image from either web or local storage and display it in the UI
        /// </summary>
        /// <param name="imageFileName">Name of the image file to load</param>
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
                // Load image from web
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

                    // Create sprite from texture and display it
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                    Image image = Instantiate(imagePrefab, imageContainer);
                    image.sprite = sprite;
                    image.gameObject.SetActive(true);
                }
            }
            else
            {
                // Load image from local StreamingAssets
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

                    // Create sprite from texture and display it
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

        /// <summary>
        /// Display a passage and its contents (text, choices, images) in the UI
        /// Supports all Twine link formats: [[Target]], [[Text|Target]], [[Text->Target]]
        /// </summary>
        /// <param name="passageTitle">The title of the passage to display</param>
        public void DisplayPassage(string passageTitle)
        {
            if (!passages.TryGetValue(passageTitle, out var passage))
            {
                Debug.LogError("Passage not found: " + passageTitle);
                return;
            }
            
            // Clear previous content
            ClearChoices();
            ClearImages();

            // Display passage text
            currentPassageTitle = passageTitle;
            passageText.text = passage.Body;

            // Create choice buttons using ParsedChoices (handles all link formats automatically)
            foreach (var choice in passage.ParsedChoices)
            {
                var choiceButton = Instantiate(choiceButtonPrefab, choiceButtonContainer);
                
                // Display the choice text on the button
                choiceButton.GetComponentInChildren<TextMeshProUGUI>().text = choice.Text;
                
                // When clicked, navigate to the target passage
                string targetPassage = choice.Target; // Capture for lambda
                choiceButton.onClick.AddListener(() => OnChoiceSelected(targetPassage, passage.Body));
            }

            // Load and display images
            foreach (var imageFileName in passage.Images)
            {
                StartCoroutine(LoadImage(imageFileName));
            }
        }

        /// <summary>
        /// Check if a "Start" passage exists and display it, or show helpful error messages
        /// </summary>
        void CheckForStartPassage(){
                if (passages.ContainsKey("Start"))
                {
                    Debug.Log("Passage 'Start' found.");
                    DisplayPassage("Start");  // Display the initial passage
                }
                else
                {
                    // Show detailed error message with troubleshooting steps
                    Debug.LogError("Passage 'Start' not found.");
                    Debug.LogError("");
                    Debug.LogError("=== HOW TO FIX ===");
                    Debug.LogError("1. Make sure your Twee file has a passage named 'Start' (case-sensitive)");
                    Debug.LogError("2. Check the StoryData section - the 'start' field should be set to 'Start'");
                    Debug.LogError("3. Verify your Twee file format:");
                    Debug.LogError("   :: Start {\"position\":\"400,100\",\"size\":\"100,100\"}");
                    Debug.LogError("   Your story text here...");
                    Debug.LogError("   [[Choice text|Target passage]]");
                    Debug.LogError("");
                    Debug.LogError("Available passages found in file:");
                    
                    if (passages.Count == 0)
                    {
                        Debug.LogError("   (No passages found - check if file was loaded correctly)");
                    }
                    else
                    {
                        // List all available passages to help debugging
                        foreach (var passageTitle in passages.Keys)
                        {
                            Debug.LogError($"   - {passageTitle}");
                        }
                    }
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
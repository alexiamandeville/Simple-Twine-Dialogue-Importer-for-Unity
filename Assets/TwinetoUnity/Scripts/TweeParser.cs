using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SimpleTwineDialogue
{
    /// <summary>
    /// TweeParser - Parser for Twine/Twee format interactive fiction files
    /// 
    /// This parser reads Twee-formatted text files and extracts passages, choices, images,
    /// and metadata (positions, sizes) for use in Unity-based interactive fiction systems.
    /// 
    /// Key Features:
    /// - Parses standard Twee passage format with :: headers
    /// - Supports multiple link formats: [[Target]], [[Text|Target]], [[Text->Target]]
    /// - Extracts position and size metadata from passage headers
    /// - Handles image references using [[Image:filename]] syntax
    /// - Separates passage body text from choices and images
    /// - Automatically skips special Twine passages (StoryTitle, StoryData)
    /// 
    /// Twee File Format Example:
    /// :: PassageName {"position":"x,y","size":"width,height"}
    /// Passage body text here.
    /// [[Image:image.png]]
    /// [[Choice text|Target passage]]
    /// 
    /// Usage:
    /// var parser = new TweeParser();
    /// var passages = parser.ParseTweeFile("path/to/file.twee");
    /// // or
    /// var passages = parser.ParseTweeFileFromText(tweeFileText);
    /// </summary>
    public class TweeParser
    {
        /// <summary>
        /// Represents a parsed choice/link from a Twee passage
        /// </summary>
        public class Choice
        {
            /// <summary>Display text shown to the user</summary>
            public string Text;
            
            /// <summary>Target passage to navigate to when selected</summary>
            public string Target;
            
            /// <summary>Original format from the Twee file (e.g., "[[Text|Target]]")</summary>
            public string OriginalFormat;
        }
        
        /// <summary>
        /// Represents a single passage from a Twee file with all its content and metadata
        /// </summary>
        public class Passage
        {
            /// <summary>The passage title/name</summary>
            public string Title;
            
            /// <summary>Optional tags associated with the passage</summary>
            public string[] Tags;
            
            /// <summary>The passage body text (with choices and images removed)</summary>
            public string Body;
            
            /// <summary>List of image filenames referenced in this passage</summary>
            public List<string> Images;
            
            /// <summary>Raw choice strings in their original format</summary>
            public List<string> Choices;
            
            /// <summary>Parsed choices with separate Text and Target properties</summary>
            public List<Choice> ParsedChoices;
            
            /// <summary>Position of the passage node in the Twine editor (x, y)</summary>
            public Vector2 Position;
            
            /// <summary>Size of the passage node in the Twine editor (width, height)</summary>
            public Vector2 Size;
        }

        /// <summary>
        /// Parse a Twee file from a file path
        /// </summary>
        /// <param name="filePath">Path to the .twee file</param>
        /// <returns>Dictionary of passages keyed by passage title</returns>
        public Dictionary<string, Passage> ParseTweeFile(string filePath)
        {
            var text = File.ReadAllText(filePath);
            return ParseTweeFileFromText(text);
        }
        
        /// <summary>
        /// Parse Twee format text and extract all passages
        /// </summary>
        /// <param name="text">The complete Twee file content as a string</param>
        /// <returns>Dictionary of passages keyed by passage title</returns>
        public Dictionary<string, Passage> ParseTweeFileFromText(string text)
        {
            var passages = new Dictionary<string, Passage>();

            // Regex to match passages in the Twee file with optional metadata
            // Pattern: :: Title [tags] {"metadata"} \n body
            // Use lookahead to stop before the next :: passage header
            var passageRegex = new Regex(@"::\s*(?<title>[^\{\[\n]+?)\s*(?:\[(?<tags>[^\]]+)\])?\s*(?:\{(?<metadata>[^\}]+)\})?\s*\n(?<body>(?:(?!^::).*\n?)*)", RegexOptions.Multiline);
            var imageRegex = new Regex(@"\[\[Image:(?<path>[^\]]+)\]\]");

            var matches = passageRegex.Matches(text);
            foreach (Match match in matches)
            {
                var title = match.Groups["title"].Value.Trim();
                
                // Skip special Twine passages that contain metadata, not story content
                if (title == "StoryTitle" || title == "StoryData")
                    continue;
                
                var tags = match.Groups["tags"].Value?.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                var metadata = match.Groups["metadata"].Value?.Trim();
                var body = match.Groups["body"].Value.Trim();

                // Parse position and size from metadata (JSON format in passage header)
                Vector2 position = Vector2.zero;
                Vector2 size = new Vector2(100, 100); // Default size

                if (!string.IsNullOrEmpty(metadata))
                {
                    // Extract position coordinates (e.g., "position":"400,200")
                    var positionMatch = Regex.Match(metadata, @"""position""\s*:\s*""(?<x>[^,]+),(?<y>[^""]+)""");
                    if (positionMatch.Success)
                    {
                        float.TryParse(positionMatch.Groups["x"].Value, out float x);
                        float.TryParse(positionMatch.Groups["y"].Value, out float y);
                        position = new Vector2(x, y);
                    }

                    // Extract size dimensions (e.g., "size":"100,100")
                    var sizeMatch = Regex.Match(metadata, @"""size""\s*:\s*""(?<width>[^,]+),(?<height>[^""]+)""");
                    if (sizeMatch.Success)
                    {
                        float.TryParse(sizeMatch.Groups["width"].Value, out float width);
                        float.TryParse(sizeMatch.Groups["height"].Value, out float height);
                        size = new Vector2(width, height);
                    }
                }

                var images = new List<string>();
                var choices = new List<string>();
                var parsedChoices = new List<Choice>();
                
                // Extract image references and remove them from the body
                var imageMatches = imageRegex.Matches(body);
                foreach (Match imageMatch in imageMatches)
                {
                    images.Add(imageMatch.Groups["path"].Value.Trim());
                    body = body.Replace(imageMatch.Value, ""); // Remove image tag from body
                }

                // Handle choices - supports three formats:
                // 1. [[Text|Target]] - pipe format (Harlowe/SugarCube style)
                // 2. [[Text->Target]] - arrow format (Twine 2 style)
                // 3. [[Target]] - simple format (text and target are the same)
                
                // Find all link patterns in the passage body
                var allLinkRegex = new Regex(@"\[\[([^\]]+)\]\]");
                var linkMatches = allLinkRegex.Matches(body);
                
                foreach (Match linkMatch in linkMatches)
                {
                    var linkContent = linkMatch.Groups[1].Value;
                    
                    // Skip image links (already processed above)
                    if (linkContent.StartsWith("Image:"))
                        continue;
                    
                    string choiceText = "";
                    string choiceTarget = "";
                    
                    // Check if it's a pipe format [[Text|Target]]
                    if (linkContent.Contains("|"))
                    {
                        var parts = linkContent.Split('|');
                        choiceText = parts[0].Trim();
                        choiceTarget = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
                    }
                    // Check if it's an arrow format [[Text->Target]]
                    else if (linkContent.Contains("->"))
                    {
                        var parts = linkContent.Split(new[] { "->" }, System.StringSplitOptions.None);
                        choiceText = parts[0].Trim();
                        choiceTarget = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
                    }
                    // Otherwise it's a simple format [[Target]]
                    else
                    {
                        choiceText = linkContent.Trim();
                        choiceTarget = linkContent.Trim();
                    }
                    
                    // Store both the raw string and parsed Choice object
                    choices.Add(linkMatch.Value.Trim());
                    parsedChoices.Add(new Choice 
                    { 
                        Text = choiceText, 
                        Target = choiceTarget,
                        OriginalFormat = linkMatch.Value.Trim()
                    });
                    
                    body = body.Replace(linkMatch.Value, ""); // Remove choice tag from body
                }

                // Create the passage object with all extracted data
                passages[title] = new Passage
                {
                    Title = title,
                    Tags = tags ?? new string[0],
                    Body = body,
                    Images = images,
                    Choices = choices,
                    ParsedChoices = parsedChoices,
                    Position = position,
                    Size = size
                };
            }

            return passages;
        }
        
        /// <summary>
        /// Parse a single choice link string to extract the display text and target passage.
        /// This is a utility method for parsing individual choice strings if needed.
        /// Supports formats: [[Target]], [[Text|Target]], [[Text->Target]]
        /// </summary>
        /// <param name="choiceString">A choice link string (e.g., "[[Click here|NextPassage]]")</param>
        /// <returns>A Choice object with Text, Target, and OriginalFormat properties</returns>
        public static Choice ParseChoice(string choiceString)
        {
            // Remove the outer brackets to get the link content
            var content = choiceString.Trim().TrimStart('[').TrimEnd(']').Trim();
            
            string text = "";
            string target = "";
            
            // Check format and split accordingly
            if (content.Contains("|"))
            {
                // Pipe format: [[Display Text|Target]]
                var parts = content.Split('|');
                text = parts[0].Trim();
                target = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
            }
            else if (content.Contains("->"))
            {
                // Arrow format: [[Display Text->Target]]
                var parts = content.Split(new[] { "->" }, System.StringSplitOptions.None);
                text = parts[0].Trim();
                target = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
            }
            else
            {
                // Simple format: [[Target]] (text and target are the same)
                text = content;
                target = content;
            }
            
            return new Choice
            {
                Text = text,
                Target = target,
                OriginalFormat = choiceString
            };
        }
    }
}

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using AnimationImporter.Boomlagoon.JSON;
using UnityEditor;
using System.IO;
using System.Text;

namespace AnimationImporter.Aseprite
{
	[InitializeOnLoad]
	public class AsepriteImporter : IAnimationImporterPlugin
	{
		// ================================================================================
		//  const
		// --------------------------------------------------------------------------------

		const string ASEPRITE_STANDARD_PATH_WINDOWS = @"C:\Program Files (x86)\Aseprite\Aseprite.exe";
		const string ASEPRITE_STANDARD_PATH_MACOSX = @"/Applications/Aseprite.app/Contents/MacOS/aseprite";

		public static string standardApplicationPath
		{
			get
			{
				if (Application.platform == RuntimePlatform.WindowsEditor)
				{
					return ASEPRITE_STANDARD_PATH_WINDOWS;
				}
				else
				{
					return ASEPRITE_STANDARD_PATH_MACOSX;
				}
			}
		}

		// ================================================================================
		//  static constructor, registering plugin
		// --------------------------------------------------------------------------------

		static AsepriteImporter ()
		{
			AsepriteImporter importer = new AsepriteImporter();
			AnimationImporter.RegisterImporter(importer, "ase", "aseprite");
		}

		// ================================================================================
		//  public methods
		// --------------------------------------------------------------------------------

		public ImportedAnimationSheet Import(AnimationImportJob job, AnimationImporterSharedConfig config)
		{
			bool successMakeSprite = CreateSpriteAtlasAndMetaFile(job);
			bool successMakePartInfo = CreateMetaInfo(job);

			if (!successMakeSprite)
			{
				Debug.LogError("Fails to make sprite sheet.");
				return null;
			}
			
			if (!successMakePartInfo)
			{
				Debug.LogError("Fails to make character part meta json.");
				return null;
			}
			
			AssetDatabase.Refresh();

			ImportedAnimationSheet animationSheet = CreateAnimationSheetFromMetaData(job, config);

			return animationSheet;
		}

		public bool IsValid()
		{
			return AnimationImporter.Instance != null && AnimationImporter.Instance.sharedData != null;
		}

		public bool IsConfigured()
		{
			return File.Exists(Path.GetFullPath(AnimationImporter.Instance.asepritePath));
		}

		// ================================================================================
		//  private methods
		// --------------------------------------------------------------------------------

		// parses a JSON file and creates the raw data for ImportedAnimationSheet from it
		private static ImportedAnimationSheet CreateAnimationSheetFromMetaData(AnimationImportJob job, AnimationImporterSharedConfig config)
		{
			var partInfo = GetMetaInfoFromJsonFile(job);
			
			if (partInfo == null)
			{
				Debug.LogError("Not found part info json file.");
				return null;
			}

			string textAssetFilename = job.directoryPathForSprites + "/" + job.name + ".json";
			TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(textAssetFilename);

			if (textAsset != null)
			{
				JSONObject jsonObject = JSONObject.Parse(textAsset.ToString());
				ImportedAnimationSheet animationSheet = GetAnimationInfo(jsonObject, partInfo);

				if (animationSheet == null)
				{
					return null;
				}

				if (!animationSheet.hasAnimations)
				{
					Debug.LogWarning("No Animations found in Aseprite file. Use Aseprite Tags to assign names to Animations.");
				}

				animationSheet.previousImportSettings = job.previousImportSettings;

				animationSheet.SetNonLoopingAnimations(config.animationNamesThatDoNotLoop);

				// delete JSON file afterwards
				// AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(textAsset));

				return animationSheet;
			}
			else
			{
				Debug.LogWarning("Problem with JSON file: " + textAssetFilename);
			}

			return null;
		}
		
		/// <summary>
		/// calls the Aseprite application which then should output a png with all sprites and a corresponding JSON
		/// </summary>
		/// <returns></returns>
		private static bool CreateSpriteAtlasAndMetaFile(AnimationImportJob job)
		{
			char delimiter = '\"';
			
			StringBuilder paramsBuilder = new StringBuilder();
			paramsBuilder.Append("--data " + delimiter + job.name + ".json" + delimiter + " --sheet " + delimiter + job.name + ".png" + delimiter + " " + job.sheetConfigParameter + " --list-tags --format json-array ");
			// paramsBuilder.Append(" --sheet-type packed ");
			// paramsBuilder.Append(" --trim ");
			// paramsBuilder.Append(" --layer head-pos ");
			paramsBuilder.Append(delimiter + job.fileName + delimiter);

			string parameters = paramsBuilder.ToString();

			if (!string.IsNullOrEmpty(job.additionalCommandLineArguments))
			{
				parameters = job.additionalCommandLineArguments + " " + parameters;
			}

			bool success = CallAsepriteCLI(AnimationImporter.Instance.asepritePath, job.assetDirectory, parameters) == 0;

			// move png and json file to subfolder
			if (success && job.directoryPathForSprites != job.assetDirectory)
			{
				// create subdirectory
				if (!Directory.Exists(job.directoryPathForSprites))
				{
					Directory.CreateDirectory(job.directoryPathForSprites);
				}

				// check and copy json file
				string jsonSource = job.assetDirectory + "/" + job.name + ".json";
				string jsonTarget = job.directoryPathForSprites + "/" + job.name + ".json";
				if (File.Exists(jsonSource))
				{
					if (File.Exists(jsonTarget))
					{
						File.Delete(jsonTarget);
					}
					File.Move(jsonSource, jsonTarget);
				}
				else
				{
					Debug.LogWarning("Calling Aseprite resulted in no json data file. Wrong Aseprite version?");
					return false;
				}

				// check and copy png file
				string pngSource = job.assetDirectory + "/" + job.name + ".png";
				string pngTarget = job.directoryPathForSprites + "/" + job.name + ".png";
				if (File.Exists(pngSource))
				{
					if (File.Exists(pngTarget))
					{
						File.Delete(pngTarget);
					}
					File.Move(pngSource, pngTarget);
				}
				else
				{
					Debug.LogWarning("Calling Aseprite resulted in no png Image file. Wrong Aseprite version?");
					return false;
				}
			}

			return success;
		}

		private static bool CreateMetaInfo(AnimationImportJob job)
		{
			char delimiter = '\"';
			
			StringBuilder paramsBuilder = new StringBuilder();
			paramsBuilder.Append(" --script-param o=");
			paramsBuilder.Append(delimiter + job.metaName + ".json" + delimiter);
			paramsBuilder.Append(" --script-param filename=");
			paramsBuilder.Append(delimiter + job.fileName + delimiter);
			paramsBuilder.Append(" --script ");
			paramsBuilder.Append(delimiter);
			paramsBuilder.Append(Path.Combine(AnimationImporter.Instance.asepriteScriptsPath, "Export Meta Json.lua"));
			paramsBuilder.Append(delimiter);

			string parameters = paramsBuilder.ToString();

			bool success = CallAsepriteCLI(AnimationImporter.Instance.asepritePath, job.assetDirectory, parameters) == 0;

			// move png and json file to subfolder
			if (success && job.directoryPathForSprites != job.assetDirectory)
			{
				// create subdirectory
				if (!Directory.Exists(job.directoryPathForSprites))
				{
					Directory.CreateDirectory(job.directoryPathForSprites);
				}

				// check and copy json file
				string jsonSource = job.assetDirectory + "/" + job.metaName + ".json";
				string jsonTarget = job.directoryPathForSprites + "/" + job.metaName + ".json";
				if (File.Exists(jsonSource))
				{
					if (File.Exists(jsonTarget))
					{
						File.Delete(jsonTarget);
					}
					File.Move(jsonSource, jsonTarget);
				}
				else
				{
					Debug.LogWarning("Calling Aseprite resulted in no json data file. Wrong Aseprite version?");
					return false;
				}
			}

			return success;
		}

		public static CharacterMetaInfo GetMetaInfoFromJsonFile(AnimationImportJob job)
		{
			string textAssetFilename = job.directoryPathForSprites + "/" + job.metaName + ".json";
			TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(textAssetFilename);

			if (textAsset == null)
			{
				return null;
			}

			var info = JsonUtility.FromJson<CharacterMetaInfo>(textAsset.text);
			return info;
		}

		private static ImportedAnimationSheet GetAnimationInfo(JSONObject root, CharacterMetaInfo partInfo)
		{
			if (root == null)
			{
				Debug.LogWarning("Error importing JSON animation info: JSONObject is NULL");
				return null;
			}

			ImportedAnimationSheet animationSheet = new ImportedAnimationSheet();

			// import all informations from JSON

			if (!root.ContainsKey("meta"))
			{
				Debug.LogWarning("Error importing JSON animation info: no 'meta' object");
				return null;
			}
			var meta = root["meta"].Obj;
			GetMetaInfosFromJSON(animationSheet, meta);

			if (GetAnimationsFromJSON(animationSheet, meta) == false)
			{
				return null;
			}

			if (GetFramesFromJSON(animationSheet, root, partInfo) == false)
			{
				return null;
			}

			animationSheet.ApplyGlobalFramesToAnimationFrames();

			return animationSheet;
		}

		private static int CallAsepriteCLI(string asepritePath, string path, string buildOptions)
		{
			string workingDirectory = Application.dataPath.Replace("Assets", "") + path;

			System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
			start.Arguments = "-b " + buildOptions;
			start.FileName = Path.GetFullPath(asepritePath);
			start.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
			start.CreateNoWindow = true;
			start.UseShellExecute = false;
			start.WorkingDirectory = workingDirectory;

			// Debug.Log("" + start.WorkingDirectory);
			// Debug.Log("" + start.FileName);
			// Debug.Log("" + start.Arguments);

			// Run the external process & wait for it to finish
			using (System.Diagnostics.Process proc = System.Diagnostics.Process.Start(start))
			{
				proc.WaitForExit();

				// Retrieve the app's exit code
				return proc.ExitCode;
			}
		}

		private static void GetMetaInfosFromJSON(ImportedAnimationSheet animationSheet, JSONObject meta)
		{
			var size = meta["size"].Obj;
			animationSheet.width = (int)size["w"].Number;
			animationSheet.height = (int)size["h"].Number;
		}

		private static bool GetAnimationsFromJSON(ImportedAnimationSheet animationSheet, JSONObject meta)
		{
			if (!meta.ContainsKey("frameTags"))
			{
				Debug.LogWarning("No 'frameTags' found in JSON created by Aseprite.");
				IssueVersionWarning();
				return false;
			}

			var frameTags = meta["frameTags"].Array;
			foreach (var item in frameTags)
			{
				JSONObject frameTag = item.Obj;
				
				if (frameTag["name"].Str.StartsWith("ev_"))
				{
					ImportedAnimationEvent ev = new ImportedAnimationEvent();
					int firstSpriteIndex = (int)(frameTag["from"].Number);
					int lastSpriteIndex = (int)(frameTag["to"].Number);

					if (firstSpriteIndex != lastSpriteIndex)
					{
						Debug.Log("Multiple frame event trigger tag is not supported: " + frameTag["name"]);
					}

					ev.name = frameTag["name"].Str.Substring(3);
					ev.spriteIndex = firstSpriteIndex;
					animationSheet.events.Add(ev);
				}
				else
				{
					ImportedAnimation anim = new ImportedAnimation();
					anim.name = frameTag["name"].Str;
					anim.firstSpriteIndex = (int)(frameTag["from"].Number);
					anim.lastSpriteIndex = (int)(frameTag["to"].Number);

					switch (frameTag["direction"].Str)
					{
						default:
							anim.direction = PlaybackDirection.Forward;
							break;
						case "reverse":
							anim.direction = PlaybackDirection.Reverse;
							break;
						case "pingpong":
							anim.direction = PlaybackDirection.PingPong;
							break;
					}

					animationSheet.animations.Add(anim);
				}
			}

			return true;
		}

		private static bool GetFramesFromJSON(ImportedAnimationSheet animationSheet, JSONObject root, CharacterMetaInfo charaMetaInfo)
		{
			var list = root["frames"].Array;

			if (list == null)
			{
				Debug.LogWarning("No 'frames' array found in JSON created by Aseprite.");
				IssueVersionWarning();
				return false;
			}

			for (int i = 0; i < list.Length; i++)
			{
				var item = list[i];
				
				ImportedAnimationFrame frame = new ImportedAnimationFrame();

				var frameValues = item.Obj["frame"].Obj;
				frame.width = (int)frameValues["w"].Number;
				frame.height = (int)frameValues["h"].Number;
				frame.x = (int)frameValues["x"].Number;
				frame.y = animationSheet.height - (int)frameValues["y"].Number - frame.height; // unity has a different coord system

				frame.duration = (int)item.Obj["duration"].Number;

				frame.headInfo = charaMetaInfo.frames[i].head;

				animationSheet.frames.Add(frame);
			}

			// 各イベントトリガーをフレームにセットする.
			foreach (var ev in animationSheet.events)
			{
				animationSheet.frames[ev.spriteIndex].eventName = ev.name;
			}

			return true;
		}

		private static void IssueVersionWarning()
		{
			Debug.LogWarning("Please use official Aseprite 1.1.1 or newer.");
		}
	}
}
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// What is kept between sessions: the levels the player has completed, the stars earned for them, and the upgrades bought with the stars.
[Serializable]
public class SaveData
{
    public int stars;
    public List<int> completed = new List<int>();   // indices of the levels completed so far
    public List<string> buffs = new List<string>();  // names of the BuffDef assets that are bought

    // the first level that is not completed yet
    public int NextLevel
    {
        get
        {
            int n = 0;
            while (completed.Contains(n)) n++;
            return n;
        }
    }
}

// One JSON file in the persistent data folder. Data loads on first use and is written whenever it changes.
public static class SaveSystem
{
    const string FileName = "save.json";

    // the level the main menu chose to play; -1 = none chosen (start at the next unfinished level)
    public static int PendingLevel = -1;

    // set by the level-complete screen: the main menu then opens the level menu at once
    public static bool OpenLevelSelect;

    static SaveData data;

    static string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    static void Load()
    {
        try
        {
            if (File.Exists(FilePath)) data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
        }
        catch (Exception e) { Debug.LogWarning("Could not read the save file: " + e.Message); }
        if (data == null) data = new SaveData();
    }

    public static void Save()
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true)); }
        catch (Exception e) { Debug.LogWarning("Could not write the save file: " + e.Message); }
    }

    public static void Reset()
    {
        data = new SaveData();
        Save();
    }
}

using System;
using System.IO;
using System.Text.Json;
using Oracle.Helpers;
using Oracle.Models;

namespace Oracle.Services
{
    /// <summary>
    /// Service for managing user profile and preferences
    /// </summary>
    public class UserProfileService
    {
        private const string PROFILE_FILENAME = "userprofile.json";
        private readonly string _profilePath;
        private UserProfile _currentProfile;
        
        public UserProfileService()
        {
            // Store profile in the application base directory
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var currentDir = Directory.GetCurrentDirectory();
            
            DebugLogger.Log("UserProfileService", $"BaseDirectory: {baseDir}");
            DebugLogger.Log("UserProfileService", $"CurrentDirectory: {currentDir}");
            
            // Try multiple locations for the profile file
            var possiblePaths = new[]
            {
                Path.Combine(currentDir, PROFILE_FILENAME),
                Path.Combine(Directory.GetParent(currentDir)?.FullName ?? currentDir, PROFILE_FILENAME),
                Path.Combine(baseDir, PROFILE_FILENAME),
                Path.Combine(Directory.GetParent(baseDir)?.FullName ?? baseDir, PROFILE_FILENAME),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", PROFILE_FILENAME)
            };
            
            DebugLogger.Log("UserProfileService", $"Checking {possiblePaths.Length} possible paths for profile:");
            foreach (var path in possiblePaths)
            {
                DebugLogger.Log("UserProfileService", $"  {path} - Exists: {File.Exists(path)}");
            }
            
            // Find the first existing profile
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _profilePath = Path.GetFullPath(path);
                    DebugLogger.Log("UserProfileService", $"✅ Found profile at: {_profilePath}");
                    break;
                }
            }
            
            // If no profile found, use the current directory
            if (string.IsNullOrEmpty(_profilePath))
            {
                _profilePath = Path.Combine(currentDir, PROFILE_FILENAME);
                DebugLogger.Log("UserProfileService", $"No existing profile found, will create at: {_profilePath}");
            }
            
            _currentProfile = LoadProfile();
        }
        
        /// <summary>
        /// Get the current user profile
        /// </summary>
        public UserProfile GetProfile() => _currentProfile;
        
        /// <summary>
        /// Get the author name
        /// </summary>
        public string GetAuthorName() 
        {
            DebugLogger.Log("UserProfileService", $"GetAuthorName() returning: '{_currentProfile.AuthorName}'");
            return _currentProfile.AuthorName;
        }
        
        /// <summary>
        /// Set the author name
        /// </summary>
        public void SetAuthorName(string name)
        {
            _currentProfile.AuthorName = name;
            SaveProfile();
        }
        
        /// <summary>
        /// Add or update a widget configuration
        /// </summary>
        public void AddOrUpdateWidget(SearchWidgetConfig widgetConfig)
        {
            // Remove existing config for the same filter path if any
            _currentProfile.ActiveWidgets.RemoveAll(w => w.FilterConfigPath == widgetConfig.FilterConfigPath);
            _currentProfile.ActiveWidgets.Add(widgetConfig);
            SaveProfile();
        }
        
        /// <summary>
        /// Remove a widget configuration
        /// </summary>
        public void RemoveWidget(string filterConfigPath)
        {
            _currentProfile.ActiveWidgets.RemoveAll(w => w.FilterConfigPath == filterConfigPath);
            SaveProfile();
        }
        
        /// <summary>
        /// Update background settings
        /// </summary>
        public void UpdateBackgroundSettings(string? theme, bool animationEnabled)
        {
            _currentProfile.BackgroundTheme = theme;
            _currentProfile.AnimationEnabled = animationEnabled;
            SaveProfile();
        }
        
        /// <summary>
        /// Update audio settings
        /// </summary>
        public void UpdateAudioSettings(int volumeLevel, bool musicEnabled)
        {
            _currentProfile.VolumeLevel = volumeLevel;
            _currentProfile.MusicEnabled = musicEnabled;
            SaveProfile();
        }
        
        /// <summary>
        /// Load profile from disk
        /// </summary>
        private UserProfile LoadProfile()
        {
            try
            {
                if (File.Exists(_profilePath))
                {
                    var json = File.ReadAllText(_profilePath);
                    var profile = JsonSerializer.Deserialize<UserProfile>(json);
                    if (profile != null)
                    {
                        DebugLogger.Log("UserProfileService", $"Loaded profile for author: {profile.AuthorName}");
                        return profile;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("UserProfileService", $"Error loading profile: {ex.Message}");
            }
            
            // Return default profile with "pifreak" as the author
            DebugLogger.Log("UserProfileService", "Creating new profile with default author: pifreak");
            return new UserProfile();
        }
        
        /// <summary>
        /// Save profile to disk
        /// </summary>
        private void SaveProfile()
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_profilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var json = JsonSerializer.Serialize(_currentProfile, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_profilePath, json);
                DebugLogger.Log("UserProfileService", $"Profile saved successfully to: {_profilePath}");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("UserProfileService", $"Error saving profile to {_profilePath}: {ex.Message}");
            }
        }
    }
}
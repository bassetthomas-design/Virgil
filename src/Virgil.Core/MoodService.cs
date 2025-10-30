*** Begin Patch
*** Update File: src/Virgil.Core/MoodService.cs
@@
-    public enum Mood
+    public enum AvatarMood
     {
         Neutral,
         Vigilant,
         Alert,
         Resting,
         Proud
     }
@@
-        private Mood _currentMood = Mood.Neutral;
+        private AvatarMood _currentMood = AvatarMood.Neutral;
@@
-        public Mood CurrentMood
+        public AvatarMood CurrentMood
         {
             get => _currentMood;
             set
             {
                 if (_currentMood != value)
                 {
                     _currentMood = value;
                     MoodChanged?.Invoke(this, EventArgs.Empty);
                 }
             }
         }
@@
-        public string GetMoodColor()
+        public string GetMoodColor()
         {
             return _currentMood switch
             {
-                Mood.Neutral  => "#007ACC",
-                Mood.Vigilant => "#FFA500",
-                Mood.Alert    => "#FF4500",
-                Mood.Resting  => "#00CED1",
-                Mood.Proud    => "#32CD32",
-                _             => "#007ACC"
+                AvatarMood.Neutral  => "#007ACC",
+                AvatarMood.Vigilant => "#FFA500",
+                AvatarMood.Alert    => "#FF4500",
+                AvatarMood.Resting  => "#00CED1",
+                AvatarMood.Proud    => "#32CD32",
+                _                  => "#007ACC"
             };
         }
*** End Patch

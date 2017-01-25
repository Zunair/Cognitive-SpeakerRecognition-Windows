using System.Text;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Web;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract.Identification;
using System.IO;
using System;
using Microsoft.ProjectOxford.SpeakerRecognition;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using jarvisWPF;

namespace SPIDIdentificationAPI_WPF_Samples
{
    class StaticClass
    {

//#if DEBUG
        internal static string WordList = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\LINKS\Data\WordList\SPID" + ".TXT";
//#else
//        private static string WordList = PublicClass.WordListPath + "SPID" + ".TXT";
//#endif

        public static SpeakerIdentificationServiceClient _serviceClient { get; private set; }

        #region Public Functions Used by LINKS
        /// <summary>
        /// This method is called by LINKS on load.
        /// </summary>
        public static void OnLoad()
        {

            //System.Diagnostics.Debugger.Break();

            _serviceClient = null;
            SubscriptionKey sK = new SubscriptionKey();
            string sKey = sK.GetSubscriptionKeyFromIsolatedStorage();

            if (sKey == null)
            {
                InputBoxResult result = InputBox.Show("Please enter the Speaker Identification Subscription key.", "Speaker Identification Subscription");
                sKey = result.Text;
                sK.SaveSubscriptionKeyToIsolatedStorage(sKey);
            }
            
            if (sKey != null || sKey != "")
                _serviceClient = new SpeakerIdentificationServiceClient(sKey);
        }

        /// <summary>
        /// Enroll speaker based on audio provided
        /// </summary>
        /// <param name="filePath">Wave file path</param>
        /// <param name="shortAudio">true or false</param>
        /// <returns>ID|EnrolledOrEnrolledAgain|FilePath</returns>
        public static async Task<string> EnrollSpeaker(string filePath, string shortAudio, string pid)
        {
            //System.Diagnostics.Debugger.Break();

            string retVal = "";

            try
            {

                if (_serviceClient == null) OnLoad();

                Guid profileId;
                if (pid == string.Empty)
                {
                    profileId = await CreateGuid();
                }
                else
                {
                    profileId = new Guid(pid);
                }

                OperationLocation processPollingLocation;
                using (Stream audioStream = File.OpenRead(filePath))
                {
                    processPollingLocation = await _serviceClient.EnrollAsync(audioStream, profileId, shortAudio.ToUpper() == "TRUE" ? true : false);
                }

                EnrollmentOperation enrollmentResult;
                int numOfRetries = 10;
                TimeSpan timeBetweenRetries = TimeSpan.FromSeconds(5.0);
                while (numOfRetries > 0)
                {
                    await Task.Delay(timeBetweenRetries);
                    enrollmentResult = await _serviceClient.CheckEnrollmentStatusAsync(processPollingLocation);

                    if (enrollmentResult.Status == Status.Succeeded)
                    {
                        if (pid != string.Empty)
                            retVal = profileId.ToString() + "|Enrolled" + "|" + filePath;
                        else
                            retVal = profileId.ToString() + "|EnrolledAgain" + "|" + filePath;

                        //retVal = "Enrolled";
                        break;
                    }
                    else if (enrollmentResult.Status == Status.Failed)
                    {
                        //throw new EnrollmentException(enrollmentResult.Message);
                        retVal = enrollmentResult.Message;
                    }
                    numOfRetries--;
                }
                if (numOfRetries <= 0)
                {
                    //throw new EnrollmentException("Enrollment operation timeout.");
                    retVal = "Enrollment operation timeout.";
                }
            }
            catch (Exception ex)
            {
                retVal = "Error: " + ex.Message;
            }
            return retVal;
        }

        /// <summary>
        /// Deletes all speaker profiles
        /// </summary>
        /// <returns></returns>
        public static async Task<string> ResetProfiles()
        {
            string retVal = string.Empty;

            try
            {
                Profile[] allProfiles = await _serviceClient.GetProfilesAsync();
                foreach (Profile p in allProfiles)
                {
                    await _serviceClient.DeleteProfileAsync(p.ProfileId);
                }
                retVal = "Completed";
            }
            catch(Exception e)
            {
                retVal = "Error: " + e.Message;
            }

            return retVal;
        }

        /// <summary>
        /// Sets subscription key and initializes it
        /// </summary>
        /// <param name="sKey"></param>
        /// <returns></returns>
        public static string SetSubscriptionKey(string sKey)
        {
            string retVal = string.Empty;

            try
            {
                SubscriptionKey sK = new SubscriptionKey();
                _serviceClient = new SpeakerIdentificationServiceClient(sKey);
                sK.SaveSubscriptionKeyToIsolatedStorage(sKey);
                retVal = "Saved Successfully";
            }
            catch (Exception e)
            {
                retVal = "Error: " + e.Message;
            }

            return retVal;
        }

        /// <summary>
        /// Get speaker identity based on audio provided
        /// </summary>
        /// <param name="filePath">Wave file path</param>
        /// <param name="shortAudio">true or false</param>
        /// <returns>ID|Confidence|FilePath</returns>
        public static async Task<string> GetIdentity(string filePath, string shortAudio)
        {
            //System.Diagnostics.Debugger.Break();

            string retVal = "";

            if (_serviceClient == null) OnLoad();

            try
            {
                if (filePath == "")
                    return ("No File Provided.");

                if (!File.Exists(filePath))
                    return ("File does not exist.");

                Profile[] allProfiles = await _serviceClient.GetProfilesAsync();
                List<Guid> testProfileIdsList = new List<Guid>();
                for (int i = 0; i < allProfiles.Length; i++)
                {
                    if (allProfiles[i].EnrollmentStatus == Microsoft.ProjectOxford.SpeakerRecognition.Contract.EnrollmentStatus.Enrolled)
                        testProfileIdsList.Add(allProfiles[i].ProfileId);
                }
                Guid[] testProfileIds = testProfileIdsList.ToArray();

                OperationLocation processPollingLocation;
                using (Stream audioStream = File.OpenRead(filePath))
                {
                    processPollingLocation = await _serviceClient.IdentifyAsync(audioStream, testProfileIds, shortAudio.ToUpper() == "TRUE" ? true : false);
                }

                IdentificationOperation identificationResponse = null;
                int numOfRetries = 10;
                TimeSpan timeBetweenRetries = TimeSpan.FromSeconds(5.0);
                while (numOfRetries > 0)
                {
                    await Task.Delay(timeBetweenRetries);
                    identificationResponse = await _serviceClient.CheckIdentificationStatusAsync(processPollingLocation);

                    if (identificationResponse.Status == Status.Succeeded)
                    {
                        break;
                    }
                    else if (identificationResponse.Status == Status.Failed)
                    {
                        throw new IdentificationException(identificationResponse.Message);
                    }
                    numOfRetries--;
                }
                if (numOfRetries <= 0)
                {
                    //throw new Exception("Identification operation timeout.");
                    throw new IdentificationException("Identification operation timeout.");
                }

                //retVal = ("Identification Done.");

                retVal = identificationResponse.ProcessingResult.IdentifiedProfileId.ToString();
                retVal += "|" + identificationResponse.ProcessingResult.Confidence.ToString() + "|" + filePath; 
            }
            catch (IdentificationException ex)
            {
                //window.Log("Speaker Identification Error: " + ex.Message);
                //retVal = "Speaker Identification Error: " + ex.Message;
                if (ex.Message != "Audio Too Short")
                    retVal = "|NoIDFound" + "|" + filePath; //await EnrollSpeaker(filePath, shortAudio, "");
            }
            catch (Exception ex)
            {
                //window.Log("Error: " + ex.Message);
                retVal = "Error: " + ex.Message;
            }

            return retVal;
        }

        /// <summary>
        /// Sample: [GetSpeakerName("xyz|High|filepath","Nice to see you again {{!Name!}}","Sounds like {{!Name!}}, am I correct?","Nice to meet you {{!Name!}}")]
        /// </summary>
        /// <param name="IdConfidenceFilepath"></param>
        /// <param name="HighConfidenceReply"></param>
        /// <param name="LowConfidenceReply"></param>
        /// <param name="OnEnrolled"></param>
        /// <returns></returns>
        public static async Task<string> GetSpeakerName(string IdConfidenceFilepath, string HighConfidenceReply, string LowConfidenceReply, string OnEnrolled)
        {
            string retVal = string.Empty;
            string tempPhrase = "";

            try
            {
                if (IdConfidenceFilepath.Contains("|"))
                {
                    string[] IdAndConfidence = IdConfidenceFilepath.Split('|');
                    string id = IdAndConfidence[0];
                    string confidence = IdAndConfidence[1].ToUpper();
                    string audioFilePath = IdAndConfidence[2];
                    string name;


                    if (IsValueFound(id))                               // If id is found in wordlist
                    {
                        if (IsWordFilled(id))                           // If name is found using id 
                        {
                            name = GetWord(id);                         // Get Name from wordlist
                        }
                        else                                            // Update Name for that id
                        {
                            tempPhrase = "Please state your name.";
                            name = GetName(tempPhrase);                 // Get Name from user                        
                            UpdateWord(name, id);                       // ID already exist, so just update the Name.
                        }
                    }
                    else if (id != string.Empty)                        // Add id and name to WordList
                    {
                        tempPhrase = "Please state your name.";
                        name = GetName(tempPhrase);                     // Get Name from user   
                        AddWord(name, id);                              // Add line for name and id.
                    }
                    else if (confidence == "NOIDFOUND")                 // if no id found, ask for name
                    {
                        tempPhrase = "Sorry I could not identify you, could you please state your name.";
                        name = GetName(tempPhrase);                     // Get Name from user   
                        if (IsWordFound(name))
                        {
                            id = GetID(name);
                            // Enroll using existing id
                            IdConfidenceFilepath = await EnrollSpeaker(audioFilePath, "true", id);
                        }
                        else
                        {
                            // Enroll new user
                            IdConfidenceFilepath = await EnrollSpeaker(audioFilePath, "true", string.Empty);
                        }

                        if (IdConfidenceFilepath.Contains("|"))
                        {
                            IdAndConfidence = IdConfidenceFilepath.Split('|');
                            id = IdAndConfidence[0];
                            confidence = IdAndConfidence[1].ToUpper();
                            audioFilePath = IdAndConfidence[2];

                            if (confidence == "ENROLLED")
                            {
                                AddWord(name, id);                      // Add line for name and id.
                            }
                        }
                        else
                        {
                            throw new Exception(IdConfidenceFilepath);
                        }
                    }
                    else
                    {
                        throw new Exception(IdConfidenceFilepath);
                    }

                    if (confidence == "NORMAL" || confidence == "LOW") // if id found with low confidence, ask for name
                    {
                        tempPhrase = LowConfidenceReply.ReplaceCaseInsensative("{{!Name!}}", name);
                        name = ConfirmName(tempPhrase, name);
                        if (IsWordFound(name))
                        {
                            id = GetID(name);
                            // Enroll using existing id
                            IdConfidenceFilepath = await EnrollSpeaker(audioFilePath, "true", id);
                        }
                        else
                        {
                            // Enroll new user
                            IdConfidenceFilepath = await EnrollSpeaker(audioFilePath, "true", string.Empty);
                        }

                        if (IdConfidenceFilepath.Contains("|"))
                        {
                            IdAndConfidence = IdConfidenceFilepath.Split('|');
                            id = IdAndConfidence[0];
                            confidence = IdAndConfidence[1].ToUpper();
                            audioFilePath = IdAndConfidence[2];

                            if (confidence == "ENROLLED")
                            {
                                AddWord(name, id);                      // Add line for name and id.
                            }
                        }
                        else
                        {
                            throw new Exception(IdConfidenceFilepath);
                        }
                    }

                    if (name != string.Empty)
                    {
                        // Replace {{!Name!}} with found name for retrieval
                        if (confidence == "HIGH" || confidence == "LOW" || confidence == "NORMAL" || confidence == "ENROLLEDAGAIN")
                        {
                            retVal = HighConfidenceReply.ReplaceCaseInsensative("{{!Name!}}", name);
                        }
                        else if (confidence == "ENROLLED")
                        {
                            retVal = OnEnrolled.ReplaceCaseInsensative("{{!Name!}}", name);
                        }
                        
                        Properties.Settings.Default.PreviousUser = Properties.Settings.Default.CurrentUser;
                        Properties.Settings.Default.CurrentUser = name;
                    }
                }
                else
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                        System.Diagnostics.Debugger.Break();
                    throw new Exception(IdConfidenceFilepath);
                }
            }
            catch(Exception e)
            {
                retVal = "Error: Sorry, " + e.Message;
            }

            return retVal;
        }

        /// <summary>
        /// Gets previously recognized user
        /// </summary>
        /// <returns></returns>
        public static string GetPreviousUser()
        {
            return Properties.Settings.Default.PreviousUser;
        }

        /// <summary>
        /// Gets recently recognized used
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentUser()
        {
            return Properties.Settings.Default.CurrentUser;
        }
        #endregion

        #region Private Functions

        /// <summary>
        /// Creates ID for new speaker
        /// </summary>
        /// <returns></returns>
        private static async Task<Guid> CreateGuid()
        {
            CreateProfileResponse creationResponse = await _serviceClient.CreateProfileAsync("en-US");
            Profile profile = await _serviceClient.GetProfileAsync(creationResponse.ProfileId);
            return profile.ProfileId;
        }

        /// <summary>
        /// Returns true if value is found in the wordlist,
        /// Creates wordlist with header if SPID wordlist is not found.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool IsValueFound(string value)
        {
            bool retVal = false;
            try
            {
                if (File.Exists(WordList))
                {
                    // Skip check if ID is blank
                    if (value == string.Empty) throw new Exception("Provided ID was Blank");

                    // Open file and return true if \t{ID} is found in the text file
                    retVal = File.ReadLines(WordList).Any(l => l.Contains("\t" + value));
                }
                else
                {
                    // Create new file with header only
                    File.WriteAllText(WordList, "Name\tSPID\r\n");
                }
            }
            catch
            {
                retVal = false;
            }

            return retVal;
        }

        /// <summary>
        /// Returns true if word is filled in column id-1 column using second+ column
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool IsWordFilled(string value)
        {
            bool retVal = false;

            try
            {
                // Skip check if provided value is blank
                if (value == string.Empty) throw new Exception("Provided value was blank.");

                // Open file and return true if value is found in first column of any row
                if (File.ReadLines(WordList).First(l => l.Contains("\t" + value)).Split('\t')[0] != string.Empty) retVal = true;
            }
            catch
            {
                retVal = false;
            }

            return retVal;
        }

        /// <summary>
        /// Returns true if word is found in column id-1 column using second+ column
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool IsWordFound(string value)
        {
            bool retVal = false;

            try
            {
                // Skip check if provided value is blank
                if (value == string.Empty) throw new Exception("Provided value was blank.");

                // Open file and return true if value is found in first column of any row
                if (File.ReadLines(WordList).First(l => l.Contains(value + "\t")).Split('\t')[0] != string.Empty) retVal = true;
            }
            catch
            {
                retVal = false;
            }

            return retVal;
        }

        /// <summary>
        /// Get first column based by querying second+ column
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string GetWord(string value)
        {
            string retVal = string.Empty;

            try
            {
                // Skip check if provided value is blank
                if (value == string.Empty) throw new Exception("Provided value was blank.");

                // Open file and return first column if value is found in second+ column of any row
                retVal = File.ReadLines(WordList).First(l => l.Contains("\t" + value)).Split('\t')[0];
            }
            catch
            {
                retVal = "Error: Value(Name) not found.";
            }

            return retVal;
        }

        /// <summary>
        /// Get second column using first column
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string GetID(string value)
        {
            string retVal = string.Empty;

            try
            {
                // Skip check if provided value is blank
                if (value == string.Empty) throw new Exception("Provided value was blank.");

                // Open file and return first column if value is found in second+ column of any row
                retVal = File.ReadLines(WordList).First(l => l.Contains(value + "\t")).Split('\t')[1];
            }
            catch
            {
                retVal = "Error: Value(ID) not found.";
            }

            return retVal;
        }

        /// <summary>
        /// Listens for speech from user and returns as string.
        /// </summary>
        /// <returns></returns>
        private static string GetName(string phraseToSpeak)
        {
            string retVal = string.Empty;

            int retryCounter = 3;
            RecognitionResult rresult;            

            while (retVal == string.Empty && retryCounter > 0)
            {
                retryCounter--;
                PublicClass.SpeechSynth.SpeakRandomPhraseSync(phraseToSpeak);
                rresult = jarvisWPF.Classes.Recognizer.GrXML.GetRecognizedSpeechResult();
                if (rresult != null && rresult.Text != string.Empty)
                {
                    retVal = rresult.Text;

                    PublicClass.SpeechSynth.SpeakRandomPhraseSync(string.Format("I heard, {0} - Is that correct?", retVal));
                    rresult = jarvisWPF.Classes.Recognizer.GrXML.GetRecognizedSpeechResult("test_en-US", "Confirmation");

                    if (rresult == null || rresult.Semantics.Value.ToString() == "No")
                    {
                        retVal = string.Empty;
                    }
                }
            }

            if (retVal == string.Empty)
            {
                do
                {
                    PublicClass.SpeechSynth.SpeakRandomPhraseSync("Please enter your name.");
                    retVal = PublicClass.ShowInputBox("Please enter your name:");
                    if (retVal == "Canceled")
                    {
                        System.Windows.MessageBoxResult r = System.Windows.MessageBox.Show("Are you sure you want to cancel?", "Confirm", System.Windows.MessageBoxButton.YesNo);
                        if (r == System.Windows.MessageBoxResult.Yes)
                        {
                            retVal = "Undefined";
                            break;
                        }
                    }
                } while (retVal == "Canceled" || retVal == string.Empty);
            }

            return retVal;
        }        
                
        /// <summary>
        /// Append word to existing wordlist
        /// </summary>
        /// <param name="name"></param>
        /// <param name="id"></param>
        private static void AddWord(string name, string id)
        {
            File.AppendAllText(WordList, name + "\t" + id);
        }

        /// <summary>
        /// Finds ID and updates word column
        /// </summary>
        /// <param name="word">first column</param>
        /// <param name="id">second+ column</param>
        private static void UpdateWord(string word, string id)
        {
            List<string> fileContents = File.ReadLines(WordList).ToList();

            string[] contents = fileContents.ToArray();
            for (int i = 0; i < contents.Length; i++)
            {
                if (contents[i].Contains("\t" + id))
                {
                    contents[i] = word + contents[i];
                    break;
                }
            }

            File.WriteAllLines(WordList, contents);
        }

        private static string ConfirmName(string phraseToSpeak, string name)
        {
            string retVal = string.Empty;
                        
            PublicClass.SpeechSynth.SpeakRandomPhraseSync(string.Format(phraseToSpeak, name));
            RecognitionResult rresult = jarvisWPF.Classes.Recognizer.GrXML.GetRecognizedSpeechResult("test_en-US", "Confirmation");
            if (rresult == null || rresult.Semantics.Value.ToString() == "No")
            {
                retVal = GetName("Please state your name.");
            }
            else
            {
                retVal = name;
            }

            return retVal;
        }

        #endregion
        
        //public static string GetIdentity(string filePath, string shortAudio)
        //{
        //    //System.Diagnostics.Debugger.Break();

        //    string retVal = "";

        //    var result = GetIdentityAsync(filePath, shortAudio).Result;
        //    retVal = result;

        //    return retVal;
        //}
    }

}

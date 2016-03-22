using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlagiarismChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Static variables to optimize the results
        /// </summary>
        static char[] DELIMITERS = { '.', '?', '\n' };
        static char WORD_SEPERATOR = ' ';
        static int MIN_ACCEPTABLE_LENGTH = 10;
        static int MIN_ACCEPTABLE_WORDS = 4;
        static int WAIT_TIME = 10;
        static int SENTENCE_WORD_LIMIT = 10;

        /// <summary>
        /// String templates
        /// </summary>
        static string ERROR_CODE_TEMPLATE = "ERROR: 0x{0}";
        static string COMPLETE_MESSAGE_TEMPLATE = "The Document has {0}% Original Content";
        static string ORIGINALITY_COUNT_TEMPLATE = "Orignality : {0}%";
        static string PLAGIARISED_COUNT_TEMPLATE = "Plagiarised : {0}";
        static string NON_PLAGIARIESD_COUNT_TEMPLATE = "Non Plagiarised : {0}";
        static string PERCENTAGE_FORMAT = "0.00";

        /// <summary>
        /// UI Control Strings
        /// </summary>
        static string STOP_TEXT = "Stop";
        static string STOPPING_TEXT = "Stopping...";

        /// <summary>
        /// Search parameters
        /// </summary>
        static string SEARCH_ENGINE_QUERY = "http://www.bing.com/search?q=%2B";
        static string NOT_FOUND_STRING = "<strong>{0}</strong>";
        static string DELIMITER = "\"";
        static string ONLINE_FIND_QUERY = "http://www.bing.com/search?q=%2B";

        /// <summary>
        /// Data to update the UI and perform the tests
        /// </summary>
        List<string> TestLines;
        volatile string lastCheckedLine;
        volatile bool isLastLinePlagiarised;

        /// <summary>
        /// The background worker to do work in the background
        /// and report and give intermittent control to the main thread
        /// </summary>
        public BackgroundWorker plagiarismChecker;
        
        /// <summary>
        /// Initializes the main window
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            //Initialize the background worker and create various event handlers
            //to perform the operations at the right time
            plagiarismChecker = new BackgroundWorker();
            plagiarismChecker.WorkerReportsProgress = true;
            plagiarismChecker.WorkerSupportsCancellation = true;
            plagiarismChecker.DoWork += PlagiarismChecker_DoWork;
            plagiarismChecker.ProgressChanged += PlagiarismChecker_ProgressChanged;
            plagiarismChecker.RunWorkerCompleted += PlagiarismChecker_RunWorkerCompleted;
        }

        /// <summary>
        /// This will run in the main thread to add final touches to the search operations
        /// </summary>
        /// <param name="sender">The background worker thread</param>
        /// <param name="e">The event args for this event
        /// </param>
        void PlagiarismChecker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //If the plagiarism checker completes checking the entire document
            //Show a message box to display the percentage of the pure content in the document
            //Otherwise reset the UI.
            if (Progress.Value == TestLines.Count) { MessageBox.Show(String.Format(COMPLETE_MESSAGE_TEMPLATE, ((double)NonPlagiarisedList.Items.Count / TestLines.Count * 100).ToString(PERCENTAGE_FORMAT)), "Test Report"); }
            else { InitializeUI(); }

            //Re-enable the start button to create another search
            Start.IsEnabled = true;

            //Reset the stop button content
            Stop.Content = STOP_TEXT;
        }

        /// <summary>
        /// This will run in the main thread and update the UI elements 
        /// according to the data provided by the background thread
        /// </summary>
        /// <param name="sender">The background worker</param>
        /// <param name="e">The progress changed event args</param>
        void PlagiarismChecker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //Update the progress bar to show that one more line has been checked
            Progress.Value += 1;

            //If the last line is plagiarised then add it to the plagiarised list
            //Else add it to the non plagiarised list
            if (isLastLinePlagiarised) { PlagiarisedList.Items.Add(lastCheckedLine); }
            else { NonPlagiarisedList.Items.Add(lastCheckedLine); }

            //Set the count of the two categories in the label
            PlagiarisedCount.Content = String.Format(PLAGIARISED_COUNT_TEMPLATE, PlagiarisedList.Items.Count);
            NonPlagiarisedCount.Content = String.Format(NON_PLAGIARIESD_COUNT_TEMPLATE, NonPlagiarisedList.Items.Count);

            //Set the originality index in the originality label
            Originality.Content = String.Format(ORIGINALITY_COUNT_TEMPLATE, ((double)NonPlagiarisedList.Items.Count / TestLines.Count * 100).ToString(PERCENTAGE_FORMAT));
        }

        /// <summary>
        /// Does the asynchronous checking of the data
        /// It is non-blocking
        /// </summary>
        /// <param name="sender">The background worker that is performing this operation</param>
        /// <param name="e">The DoWorkEventArgs</param>
        void PlagiarismChecker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                //for every test line in the TestLines do the followinf operations
                //Set the last checked line to the line being checked
                //Set the Plagiarism flag for that line
                //Report progrss and sleep for 1ms to pass the control to the main thread ( UI thread )
                foreach(var testLine in TestLines)
                {
                    //Proceed only when there is no cancellation pending
                    if (plagiarismChecker.CancellationPending == false)
                    {
                        lastCheckedLine = testLine;
                        isLastLinePlagiarised = IsPlagiarised(testLine);
                        plagiarismChecker.ReportProgress(1);
                        System.Threading.Thread.Sleep(WAIT_TIME);
                    }
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, String.Format(ERROR_CODE_TEMPLATE, ex.HResult.ToString().Remove('-')));
            }
        }

        /// <summary>
        /// This is the event handler that will start the checking process
        /// It handles the click event from the button "Start"
        /// </summary>
        /// <param name="sender">The button Start</param>
        /// <param name="e">The routed event args</param>
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            //Store the validated data in a list container
            TestLines = ValidateData(Content.Text);
            
            //Initialize the UI elements
            InitializeUI();

            //Start checking the test data
            plagiarismChecker.RunWorkerAsync();
        }

        /// <summary>
        /// Initilaizes the UI by setting it to default state
        /// </summary>
        private void InitializeUI()
        {
            //Disable the start button
            Start.IsEnabled = false;

            //Reset the progress bar
            Progress.Minimum = 0;
            Progress.Maximum = TestLines.Count;
            Progress.Value = 0;

            //Clear the cache in the list boxes
            PlagiarisedList.Items.Clear();
            NonPlagiarisedList.Items.Clear();

            //Initialize the label to reflect that the search has not started yet
            PlagiarisedCount.Content = String.Format(PLAGIARISED_COUNT_TEMPLATE, 0);
            NonPlagiarisedCount.Content = String.Format(NON_PLAGIARIESD_COUNT_TEMPLATE, 0);

            //Reset the originality index
            Originality.Content = String.Format(ORIGINALITY_COUNT_TEMPLATE, 0);

            //Reset the stop button
            Stop.Content = STOP_TEXT;
            Stop.IsEnabled = true;
        }

        /// <summary>
        /// Calls the appropriate data validator
        /// </summary>
        /// <param name="content">The content to be validated</param>
        /// <returns>The list of validated string</returns>
        private List<string> ValidateData(string content) 
        {
            //Clean the recurring symbols
            content = Regex.Replace(content.Replace('\n', WORD_SEPERATOR)
                                           .Replace('\r', WORD_SEPERATOR), @"[ ]{2,}", @" ", RegexOptions.None);
 
            //Select a cleaning algorith based on the user option
            //Returned the cleaned data
            return FullSentence.IsChecked == true ? ValidateData1(content) : ValidateData2(content); 
        }

        /// <summary>
        /// This will get the data and clean it
        /// To a format more suitable for checking online
        /// </summary>
        /// <param name="content">The content which is to be formatted and cleaned</param>
        /// <returns>Returns the list containing the data to be checked</returns>
        private List<string> ValidateData1(string content)
        {
            //Initialize the raw data
            List<string> contentLines = new List<string>(content.Split(DELIMITERS));
            List<string> testLines = new List<string>();

            //Clean the data
            //Simple cleaning to filter all the sentences which have less than 10 letters in it
            //Added to this use a regex to remove multiple space into a single space
            foreach (var contentLine in contentLines)
            {
                if (contentLine.Length >= MIN_ACCEPTABLE_LENGTH && contentLine.Split(WORD_SEPERATOR).Length > MIN_ACCEPTABLE_WORDS) 
                { 
                    testLines.Add(contentLine);
                }
            }

            //Return the resultant list
            return testLines;
        }
        
        /// <summary>
        /// This will get the data and clean it
        /// To a format more suitable for checking online
        /// </summary>
        /// <param name="content">The content which is to be formatted and cleaned</param>
        /// <returns>Returns the list containing the data to be checked</returns>
        private List<string> ValidateData2(string content)
        {
            //Get the individual words
            //and intialize the container for the sentences
            List<string> words = new List<string>(content.Split(WORD_SEPERATOR));
            List<string> testLines = new List<string>();

            //For every word do the following
            for (int i = 0; i < words.Count; )
            {
                string temp = "";
                for(int j=0; j<SENTENCE_WORD_LIMIT && i<words.Count; j++, i++)
                {
                    temp += words[i] + WORD_SEPERATOR;
                }
                testLines.Add(temp);
            }

            //Return the resultant list
            return testLines;
        }

        /// <summary>
        /// This will check whether a string exists in the Bing Search Results or not
        /// </summary>
        /// <param name="textSentance">The sentence to be checked online</param>
        /// <returns>A boolean variable which tells us whether the input sentence is plagiarised or not</returns>
        public bool IsPlagiarised(string textSentance)
        {
            //The format of the URL to send a request to Bing
            string url = SEARCH_ENGINE_QUERY + DELIMITER + textSentance.Replace(" ", "+") + DELIMITER;

            try
            {
                //Download the search response
                WebClient client = new WebClient();
                string result = Regex.Replace(client.DownloadString(url), @"[^0-9a-zA-Z\n<>]+", "");
                string query = Regex.Replace(String.Format(NOT_FOUND_STRING, textSentance), @"[^0-9a-zA-Z\n<>]+", "");

                //Check to see if it contains no results in it
                //Return true if it is avilable in the Bing search results
                //Return false if it is not avilable in the Bing search results
                if (result.Contains(query) == false) { return false; }
                else { return true; }
            } catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                throw e;
            }
        }

        /// <summary>
        /// Searches the clicked item online in a search engine
        /// </summary>
        /// <param name="sender">The list box that is double clicked</param>
        /// <param name="e">The event args</param>
        private void ListBox_ItemClick(object sender, MouseButtonEventArgs e)
        {
            ListBox senderList = sender as ListBox;

            //If an item is clicked. Open it in a web browser.
            if (senderList.SelectedIndex >= 0) { Process.Start(ONLINE_FIND_QUERY + DELIMITER + DELIMITER + senderList.SelectedItem.ToString().Replace(' ', '+') + DELIMITER + DELIMITER); }
        }

        /// <summary>
        /// Stop the checking process 
        /// and reinitialize the UI 
        /// </summary>
        /// <param name="sender">The Stop Button</param>
        /// <param name="e">The RoutedEventArgs</param>
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            //If the background thread is running then try to stop the thread
            if (plagiarismChecker != null && plagiarismChecker.IsBusy)
            {
                //Indicate the status
                Stop.Content = STOPPING_TEXT;
                Stop.IsEnabled = false;

                //Cancel the checker async and wait to transfer the control
                plagiarismChecker.CancelAsync();
                System.Threading.Thread.Sleep(WAIT_TIME);
            }
        }

        /// <summary>
        /// Double click event to select all the text in the data field
        /// </summary>
        /// <param name="sender">The object clicked</param>
        /// <param name="e">The event args</param>
        private void Content_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //Select all the text in the content text box upon a mouse double click
            Content.SelectAll(); 
        }
    }
}
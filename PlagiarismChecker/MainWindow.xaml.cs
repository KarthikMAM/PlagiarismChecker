using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Windows.Threading;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Diagnostics;

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
        static int MIN_ACCEPTABLE_LENGTH = 10;
        static int MIN_ACCEPTABLE_WORDS = 4;
        static int WAIT_TIME = 10;

        static string SEARCH_ENGINE_QUERY = "http://www.bing.com/search?q=";
        static string NOT_FOUND_STRING = "No results found for ";
        static string DELIMITER = "\"";

        static string ONLINE_FIND_QUERY = "http://www.google.com/search?q=";

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
            //Show a message box to display the percentage of the pure content in the document
            MessageBox.Show("The content is " + ((double)NonPlagiarisedList.Items.Count / TestLines.Count * 100).ToString("0.00") + "% Non-Plagiarised", "Test Report");

            //Re-enable the start button to create another search
            Start.IsEnabled = true;
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
            PlagiarisedCount.Content = "Plagiarised : " + PlagiarisedList.Items.Count;
            NonPlagiarisedCount.Content = "Non Plagiarised : " + NonPlagiarisedList.Items.Count;

            //Set the originality index in the originality label
            Originality.Content = "Orignality : " + ((double)NonPlagiarisedList.Items.Count / TestLines.Count * 100).ToString("0.00") + "%";
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
                MessageBox.Show(ex.Message, "ERR" + ex.HResult);
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
            PlagiarisedCount.Content = "Plagiarised : " + "0";
            NonPlagiarisedCount.Content = "Non Plagiarised : " + "0";

            //Reset the originality index
            Originality.Content = "Originality: 0%";
        }

        /// <summary>
        /// This will get the data and clean it
        /// To a format more suitable for checking online
        /// </summary>
        /// <param name="content">The content which is to be formatted and cleaned</param>
        /// <returns>Returns the list containing the data to be checked</returns>
        private List<string> ValidateData(string content)
        {
            //Initialize the raw data
            List<string> contentLines = new List<string>(content.Split(DELIMITERS));
            List<string> testLines = new List<string>();

            //Clean the data
            //Simple cleaning to filter all the sentences which have less than 10 letters in it
            //Added to this use a regex to remove multiple space into a single space
            foreach (var contentLine in contentLines)
            {
                if (contentLine.Length >= MIN_ACCEPTABLE_LENGTH && contentLine.Split(' ').Length - 1 >= MIN_ACCEPTABLE_WORDS)
                {
                    testLines.Add(Regex.Replace(contentLine, @"[ ]{2,}", @" ", RegexOptions.None));
                }
            }

            //Return the resultant list
            return testLines;
        }

        /// <summary>
        /// This will check whether a string exists in the Google Search Results or not
        /// </summary>
        /// <param name="textSentance">The sentence to be checked online</param>
        /// <returns>A boolean variable which tells us whether the input sentence is plagiarised or not</returns>
        public bool IsPlagiarised(string textSentance)
        {
            //The format of the URL to send a request to Google
            string url = Uri.EscapeUriString(SEARCH_ENGINE_QUERY + DELIMITER + textSentance + DELIMITER);

            //Download the search response
            WebClient client = new WebClient();
            string result = client.DownloadString(url);

            //Check to see if it contains no results in it
            //Return true if it is avilable in the google search results
            //Return false if it is not avilable in the google search results
            if (result.Contains(NOT_FOUND_STRING) == true) { return false; }
            else { return true; }
        }

        private void ListBox_ItemClick(object sender, MouseButtonEventArgs e)
        {
            ListBox senderList = sender as ListBox;

            if(senderList.SelectedIndex >= 0)
            {
                Process.Start(ONLINE_FIND_QUERY + DELIMITER + senderList.SelectedItem.ToString() + DELIMITER);
            }
        }
    }
}

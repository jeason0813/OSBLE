﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

using FileUploader.OsbleServices;
using FileUploader.Controls;
using System.IO.IsolatedStorage;

namespace FileUploader
{
    public partial class UploaderPage : Page
    {
        private string authToken = "";
        private string OsbleUrl = "http://localhost:17532";
        public string RootPath;

        //reference to our web service
        private UploaderWebServiceClient client = new UploaderWebServiceClient();

        //used for pulling files from the server
        private WebClient fileClient = new WebClient();

        // stores 
        private KeyValuePair<int, string> course;

        public string LocalPath
        {
            get
            {
                return LocalFileTextBox.Text;
            }
            set
            {
                LocalFileTextBox.Text = value;

                //catch root folder issues on windows systems
                if (LocalPath.Length > 0 && LocalPath.Substring(LocalPath.Length - 1) == ":")
                {
                    LocalPath += "\\";
                }
                
            }
        }

        public UploaderPage(string authenticationToken)
        {
            InitializeComponent();
            
            authToken = authenticationToken;

            //get our local path
            LocalPath =  GetLastLocalPath();
            RootPath = LocalPath;

            //listeners for our web service
            client.GetFileListCompleted += new EventHandler<GetFileListCompletedEventArgs>(GetFileListCompleted);
            client.GetValidUploadLocationsCompleted += new EventHandler<GetValidUploadLocationsCompletedEventArgs>(GetValidUploadLocationsCompleted);
            client.DeleteFileCompleted += new EventHandler<DeleteFileCompletedEventArgs>(SelectionChanged);
            client.UpdateListingOrderCompleted += new EventHandler<System.ComponentModel.AsyncCompletedEventArgs>(client_UpdateListingOrderCompleted);

            //local event listeners
            SyncButton.Click += new RoutedEventHandler(SyncButton_Click);
            SendFileButton.Click += new RoutedEventHandler(SendFileButton_Click);
            LocalFileList.EmptyDirectoryEncountered += new EventHandler(LocalFileList_EmptyDirectoryEncountered);
            LocalFileList.ParentDirectoryRequest += new EventHandler(LocalFileList_ParentDirectoryRequest);
            LocalFileTextBox.KeyUp += new KeyEventHandler(LocalFileTextBox_KeyUp);
            UploadLocation.SelectionChanged += new SelectionChangedEventHandler(SelectionChanged);
            UpButton.Click += new RoutedEventHandler(UpButton_Click);
            DownButton.Click += new RoutedEventHandler(DownButton_Click);
            RemoveRemoteSelectionButton.Click += new RoutedEventHandler(RemoveRemoteSelectionButton_Click);
            DownloadRemoteFileButton.Click += new RoutedEventHandler(DownloadRemoteFileButton_Click);
            fileClient.OpenReadCompleted += new OpenReadCompletedEventHandler(DownloadRemoteFileCompleted);

            //get the remote server file list
            client.GetValidUploadLocationsAsync(authToken);
            
            //get the local files
            LocalFileList.DataContext = FileOperations.BuildLocalDirectoryListing(LocalPath);
        }

        void client_UpdateListingOrderCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            DownButton.IsEnabled = true;
            UpButton.IsEnabled = true;
        }


        void DownButton_Click(object sender, RoutedEventArgs e)
        {
            DownButton.IsEnabled = false;
            UpButton.IsEnabled = false;
            RemoteFileList.MoveSelectionDown();
            client.UpdateListingOrderAsync(RemoteFileList.DataContext, course.Key, authToken);
        }

        void DownloadRemoteFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (RemoteFileList.SelectedItem is FileListing)
            {
                //send a request to download the selected file
                FileListing fi = RemoteFileList.SelectedItem as FileListing;
                Uri uri = new Uri(OsbleUrl + fi.FileUrl);
                RemoteFileList.IsEnabled = false;
                DownloadRemoteFileButton.IsEnabled = false;
                DownloadRemoteFileButton.Content = "Downloading...";
                fileClient.OpenReadAsync(uri);
            }
        }

        void DownloadRemoteFileCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            Stream remoteStream = e.Result;
            AbstractListing listing = RemoteFileList.SelectedItem;
            
            //write the file to the current local path, using the server file name
            string writePath = Path.Combine(LocalPath, listing.Name);
            FileStream localStream = new FileStream(writePath, FileMode.Create);
            
            //what a cool function!
            remoteStream.CopyTo(localStream);

            //close everything up, re-enable various buttons
            remoteStream.Close();
            localStream.Close();
            RemoteFileList.IsEnabled = true;
            DownloadRemoteFileButton.IsEnabled = true;
            DownloadRemoteFileButton.Content = "Download Selected File";

            //refresh the local data context
            LocalFileList.DataContext = FileOperations.BuildLocalDirectoryListing(LocalPath);
        }

        void GetFileListCompleted(object sender, GetFileListCompletedEventArgs e)
        {
            RemoteFileList.DataContext = e.Result;
        }

        void GetValidUploadLocationsCompleted(object sender, GetValidUploadLocationsCompletedEventArgs e)
        {
            UploadLocation.Items.Clear();
            UploadLocation.ItemsSource = e.Result;
            UploadLocation.SelectedIndex = 0;
        }        

        /// <summary>
        /// Returns the path of the last path on this machine that was synced with the server
        /// </summary>
        /// <returns></returns>
        private string GetLastLocalPath()
        {
            using (IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (store.FileExists("localpath.txt"))
                {
                    using (IsolatedStorageFileStream stream = store.OpenFile("localpath.txt", FileMode.Open))
                    {
                        StreamReader reader = new StreamReader(stream);
                        string path = reader.ReadLine().Trim();
                        reader.Close();
                        return path;
                    }
                }
                else
                {
                    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
            }
        }

        //allows the user to quick navigate to a desired location
        void LocalFileTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LocalPath = (sender as TextBox).Text;
                //LocalFileList_EmptyDirectoryEncountered(new FileList { DataContext = new DirectoryListing() { Name = "" } }, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Moves up one directory on the local machine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LocalFileList_ParentDirectoryRequest(object sender, EventArgs e)
        {
            FileList list = sender as FileList;
            list.ClearPreviousDirectories();
            DirectoryListing listing = list.DataContext;
            LocalPath = LocalPath.Substring(0, LocalPath.LastIndexOf('\\'));
            LocalFileList.DataContext = FileOperations.BuildLocalDirectoryListing(LocalPath);
        }

        /// <summary>
        /// Moves down into the selected directory on the local machine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LocalFileList_EmptyDirectoryEncountered(object sender, EventArgs e)
        {
            FileList list = sender as FileList;
            list.ClearPreviousDirectories();
            DirectoryListing listing = list.DataContext;
            LocalPath = Path.Combine(LocalPath, listing.Name);
            LocalFileList.DataContext = FileOperations.BuildLocalDirectoryListing(LocalPath);
        }

        /// <summary>
        /// Will send the currently selected file to the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            //as long as something was selected, we're good to go
            if (LocalFileList.SelectedItem != null)
            {
                //two cases: if a FileListItem is selected, then we need to smash it
                //into a DirectoryListing.  If it's a DirectoryListing, then we're good
                //to go.
                DirectoryListing dl = new DirectoryListing() { Files = new ObservableCollection<FileListing>(), Directories = new ObservableCollection<DirectoryListing>() };
                if (LocalFileList.SelectedItem is FileListing)
                {
                    dl.Files.Add(LocalFileList.SelectedItem as FileListing);
                }
                else if(LocalFileList.SelectedItem is DirectoryListing)
                {
                    //if it's a directory listing, then we need to send the inner contents of the directory
                    dl.Directories.Add(FileOperations.BuildLocalDirectoryListing(LocalFileList.SelectedItem.AbsolutePath, false, true));
                }

                //almost exaxtly the same code as in "SyncButton_Click".  Might want to refactor at
                //some point
                UploadingModal uploader = new UploadingModal();
                uploader.Listing = dl;
                uploader.CourseId = course.Key;
                uploader.AuthToken = authToken;
                uploader.BeginUpload();
                uploader.Show();
                uploader.Closed += new EventHandler(uploader_Closed);
            }
        }

        /// <summary>
        /// Saves the last location synced to the server
        /// </summary>
        /// <param name="path"></param>
        private void SavelastLocalPath(string path)
        {
            using (IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (IsolatedStorageFileStream stream = store.OpenFile("localpath.txt", FileMode.Create))
                {
                    StreamWriter writer = new StreamWriter(stream);
                    writer.Write(path);
                    writer.Close();
                }
            }
        }

        void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            //save the last local path
            SavelastLocalPath(LocalPath);

            DirectoryListing listing = FileOperations.BuildLocalDirectoryListing(LocalPath, false, true);
            UploadingModal uploader = new UploadingModal();
            uploader.Listing = listing;
            uploader.CourseId = course.Key;
            uploader.AuthToken = authToken;
            uploader.BeginUpload();
            uploader.Show();
            uploader.Closed += new EventHandler(uploader_Closed);
        }

        /// <summary>
        /// Call to remove the selected item from the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void RemoveRemoteSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            client.DeleteFileAsync(RemoteFileList.SelectedItem.AbsolutePath, course.Key, authToken);
        }

        void uploader_Closed(object sender, EventArgs e)
        {
            SelectionChanged(this, null);
        }

        void UpButton_Click(object sender, RoutedEventArgs e)
        {
            DownButton.IsEnabled = false;
            UpButton.IsEnabled = false;
            RemoteFileList.MoveSelectionUp();
            client.UpdateListingOrderAsync(RemoteFileList.DataContext, course.Key, authToken);
        }

        void SelectionChanged(object sender, EventArgs e)
        {
            //very hack-ish at the moment, may need to revise
            course = (KeyValuePair<int, string>)UploadLocation.SelectedValue;
            client.GetFileListAsync(course.Key, authToken);
        }
    }
}

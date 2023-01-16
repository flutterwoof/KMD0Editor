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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Markup;
using System.IO;
using Microsoft.Win32;
using System.Net.Sockets;
using System.DirectoryServices.ActiveDirectory;
using System.Collections;
using System.Globalization;
using System.Collections.ObjectModel;

namespace KMD0Editor
{


    public class KMD0Row
    {
        public bool RestartOnLLChange { get; set; }

        public byte[] UnknownBytes;

        public string UnknownBytesString
        {
            get
            {
                return Convert.ToHexString(UnknownBytes);
            }
            set
            {
                int NumberChars = value.Length;
                byte[] bytes = new byte[NumberChars / 2];
                for (int i = 0; i < NumberChars; i += 2)
                    bytes[i / 2] = Convert.ToByte(value.Substring(i, 2), 16);
                UnknownBytes = bytes;
            }
        }



        public BitArray MuteTracks;

        public byte[] MuteTracksBytes
        {
            get
            {
                byte[] bytes = new byte[4];
                MuteTracks.CopyTo(bytes, 0);
                return bytes;
            }
        }
        public string MuteTracksString
        {
            get
            {
                List<int> trackintegers = new List<int>();
                int i = 0;
                foreach (bool bit in MuteTracks)
                {
                    if (bit)
                    {
                        trackintegers.Add(i);
                    }
                    i++;
                }

                return string.Join(",", trackintegers);
            }
            set
            {
                List<int> trackintegers = new List<int>();

                if (value != "")
                {
                    trackintegers = value.Split(',').Select(int.Parse).ToList();
                }

                MuteTracks.SetAll(false);

                foreach (int bit in trackintegers)
                {
                    if (bit < 32)
                    {
                        MuteTracks[bit] = true;
                    }
                }
            }
        }

        public KMD0Row(byte[] RowBytes)
        {
            RestartOnLLChange = Convert.ToBoolean(RowBytes[0]);
            UnknownBytes = RowBytes[1..8]; // extract bytes 1 to 7 from row

            MuteTracks = new BitArray(RowBytes[8..12]);
        }

        public byte[] KMD0RowBytes
        {
            get
            {
                byte[] bytes = new byte[12];
                bytes[0] = Convert.ToByte(RestartOnLLChange);
                UnknownBytes.CopyTo(bytes, 1);
                MuteTracksBytes.CopyTo(bytes, 8);
                return bytes;
            }
        }
    }

    public class UnknownBytesValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string IncomingUnknownBytesString = value as string;

            if (IncomingUnknownBytesString.Length != 14)
            {
                return new ValidationResult(false, "Too short");
            }
            try
            {
                int NumberChars = IncomingUnknownBytesString.Length;
                byte[] bytes = new byte[NumberChars / 2];
                for (int i = 0; i < NumberChars; i += 2)
                    bytes[i / 2] = Convert.ToByte(IncomingUnknownBytesString.Substring(i, 2), 16);
            }
            catch
            {
                return new ValidationResult(false, "Can't split to integers");
            }

            return new ValidationResult(true, "");
        }
    }

    public class MuteTracksValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string IncomingMuteTracksString = value as string;

            if (IncomingMuteTracksString != "")
            {
                try
                {
                    List<int> trackintegers = new List<int>();
                    trackintegers = IncomingMuteTracksString.Split(',').Select(int.Parse).ToList();

                    foreach (int bit in trackintegers)
                    {
                        if (bit >= 32)
                        {
                            return new ValidationResult(false, "Value too high");
                        }
                    }
                }
                catch
                {
                    return new ValidationResult(false, "Can't split to integers");
                }
            }

            return new ValidationResult(true, "");
        }
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {


        public ObservableCollection<KMD0Row> KMD0Rows = new ObservableCollection<KMD0Row>();

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            KMD0RowView.ItemsSource = KMD0Rows;
        }

        private void LoadKMD0_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog loadKMD0Dialog = new OpenFileDialog();
            loadKMD0Dialog.Filter = "All Files (*.*)|*.*";
            loadKMD0Dialog.Title = "Select a file to load KMD0 from";

            bool? result = loadKMD0Dialog.ShowDialog();

            if (result == true)
            {
                LoadKMD0FromKMD0(loadKMD0Dialog.FileName);
            }
        }
        private void LoadMID_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog loadMIDDialog = new OpenFileDialog();
            loadMIDDialog.Filter = "All Files (*.*)|*.*";
            loadMIDDialog.Title = "Select a MIDI file to load KMD0 from";

            bool? result = loadMIDDialog.ShowDialog();

            if (result == true)
            {
                LoadKMD0FromMidi(loadMIDDialog.FileName);
            }
        }

        private void SaveKMD0_Click(object sender, RoutedEventArgs e)
        {
            // don't wanna save without at least one row
            if (KMD0Rows.Count == 0)
            {
                MessageBox.Show("Won't save with zero rows.");
                return;
            }

            SaveFileDialog saveKMD0Dialog = new SaveFileDialog();
            saveKMD0Dialog.Filter = "All Files (*.*)|*.*";
            saveKMD0Dialog.Title = "Select a file to save KMD0 to";

            bool? result = saveKMD0Dialog.ShowDialog();

            if (result == true)
            {
                SaveKMD0ToKMD0(saveKMD0Dialog.FileName);
                MessageBox.Show("Save complete.");
            }
        }

        private void AppendMID_Click(object sender, RoutedEventArgs e)
        {
            // don't wanna save without at least one row
            if (KMD0Rows.Count == 0)
            {
                MessageBox.Show("Won't save with zero rows.");
                return;
            }

            SaveFileDialog saveMIDDialog = new SaveFileDialog();
            saveMIDDialog.Filter = "All Files (*.*)|*.*";
            saveMIDDialog.Title = "Select a MIDI file to save KMD0 to";

            bool? result = saveMIDDialog.ShowDialog();

            if (result == true)
            {
                SaveKMD0ToMidi(saveMIDDialog.FileName);
                MessageBox.Show("Save complete.");
            }
        }



        private void LoadKMD0FromKMD0(string kmd0Path)
        {
            this.Title = "KMD0 Editor (last loaded from " + System.IO.Path.GetFileName(kmd0Path) + ")";
            KMD0Rows.Clear();
            var fs = new FileStream(kmd0Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            ParseKMD0(fs, true);
        }

        private void LoadKMD0FromMidi(string midiPath)
        {
            this.Title = "KMD0 Editor (last loaded from " + System.IO.Path.GetFileName(midiPath) + ")";
            KMD0Rows.Clear();
            var fs = new FileStream(midiPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (SeekMIDIToKMD0Spot(fs))
            {
                ParseKMD0(fs, false);
            }
        }





        private void SaveKMD0ToKMD0(string kmd0Path)
        {
            this.Title = "KMD0 Editor (last saved to " + System.IO.Path.GetFileName(kmd0Path) + ")";
            var fs = new FileStream(kmd0Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            WriteKMD0ToFile(fs);
        }

        private void SaveKMD0ToMidi(string midiPath)
        {
            this.Title = "KMD0 Editor (last saved to " + System.IO.Path.GetFileName(midiPath) + ")";
            var fs = new FileStream(midiPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            if (SeekMIDIToKMD0Spot(fs))
            {
                // TODO remove existing KMD0 region
                WriteKMD0ToFile(fs);
            }
        }


        private void WriteKMD0ToFile(FileStream fs)
        {
            byte KMD0Padding = 0;

            if (fs.Position % 4 != 0) // if it's already 0 (as in no padding), doing this will just turn it to 4 and we don't want that
            {
                KMD0Padding = (byte)(4 - (fs.Position % 4));
            }

            fs.SetLength(fs.Position); // remove old KMD0
            
            fs.Write(new byte[] { 75, 77, 68, 48 }); // write KMD0 header

            long LengthPosition = fs.Position;
            fs.Seek(4, SeekOrigin.Current);

            for (int i = 0; i < KMD0Padding; i++)
            {
                fs.WriteByte(0xFF);
            }

            foreach (KMD0Row row in KMD0Rows)
            {
                fs.Write(row.KMD0RowBytes);
            }

            fs.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0 }); // end of file


            // find length and write to file

            uint KMD0BlockLength = (uint)(fs.Position - (LengthPosition + 4));
            fs.Seek(LengthPosition, SeekOrigin.Begin);

            byte[] KMD0BlockLengthBytes = BitConverter.GetBytes(KMD0BlockLength);
            if (BitConverter.IsLittleEndian) // MIDI is big endian, gotta reverse bytes
                Array.Reverse(KMD0BlockLengthBytes);

            fs.Write(KMD0BlockLengthBytes);


            fs.Flush();
            fs.Close();
        }

        private bool SeekMIDIToKMD0Spot(FileStream fs)
        {
            byte[] MIDIHeader = new byte[4];
            fs.Read(MIDIHeader);

            if (Encoding.UTF8.GetString(MIDIHeader) != "MThd")
            {
                MessageBox.Show("File is not MIDI.");
                return false;
            }

            fs.Seek(10, SeekOrigin.Begin); // skip to number of tracks in MIDI header

            // find out how many MIDI tracks there are
            byte[] NumberOfTracksBytes = new byte[2];
            fs.Read(NumberOfTracksBytes);

            if (BitConverter.IsLittleEndian) // MIDI is big endian, gotta reverse these bytes if we're on a little endian system because BitConverter is going to read them in little endian
                Array.Reverse(NumberOfTracksBytes);

            uint NumberOfTracks = BitConverter.ToUInt16(NumberOfTracksBytes, 0);

            fs.Seek(2, SeekOrigin.Current); // skip division value in MIDI header

            // skip past all the tracks 
            for (int i = 0; i < NumberOfTracks; i++)
            {
                fs.Seek(4, SeekOrigin.Current); // skip "MTrk" in track chunk header

                byte[] TrackLengthBytes = new byte[4];
                fs.Read(TrackLengthBytes);

                if (BitConverter.IsLittleEndian) // MIDI is big endian, see above
                    Array.Reverse(TrackLengthBytes);

                uint TrackLength = BitConverter.ToUInt32(TrackLengthBytes, 0);

                fs.Seek(TrackLength, SeekOrigin.Current);
            }
            return true;
        }

        private void ParseKMD0(FileStream fs, bool GuessPadding)
        {
            byte KMD0Padding = 0;

            long StartPosition = fs.Position;

            


            byte[] KMD0Header = new byte[4];
            fs.Read(KMD0Header);

            if (Encoding.UTF8.GetString(KMD0Header) != "KMD0")
            {
                MessageBox.Show("File doesn't contain KMD0 region.");
                return;
            }


            // probably gonna go unused, but just reading it anyway
            byte[] KMD0LengthBytes = new byte[4];
            fs.Read(KMD0LengthBytes);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(KMD0LengthBytes);

            uint KMD0Length = BitConverter.ToUInt32(KMD0LengthBytes, 0);



            // if it's a .kmd0, we guess the padding based on how many FF bytes there are (because the test .kmd0 file has its padding bytes)
            if (GuessPadding)
            {
                bool PaddingLengthFound = false;

                while (!PaddingLengthFound)
                {
                    if (KMD0Padding > 3)
                    {
                        MessageBox.Show("Too many padding bytes at start of KMD0 - can't read this!");
                        return;
                    }

                    byte[] padding = new byte[1];
                    fs.Read(padding);
                    if (padding[0] == (byte)0xFF)
                    {
                        KMD0Padding++;
                    }
                    else
                    {
                        fs.Position--;
                        PaddingLengthFound = true;
                    }
                }
            }
            // if it's a MIDI we should calculate the padding based on DWORD alignment
            else
            {
                if (StartPosition % 4 != 0) // if it's already 0 (as in no padding), doing this will just turn it to 4 and we don't want that
                {
                    KMD0Padding = (byte)(4 - (StartPosition % 4));
                }
                fs.Seek(KMD0Padding, SeekOrigin.Current);
            }


            bool EndOfFile = false;

            int RowIterator = 0;

            while (!EndOfFile && RowIterator <= 200)
            {
                byte[] KMD0PureRow = new byte[12];
                fs.Read(KMD0PureRow);
                if (Convert.ToHexString(KMD0PureRow) == "FFFFFFFF0000000000000000")
                {
                    EndOfFile = true;
                }
                else
                {
                    KMD0Rows.Add(new KMD0Row(KMD0PureRow));
                }
                RowIterator++;
            }
            
            if (RowIterator >= 200)
            {
                MessageBox.Show("Way too many rows - file read must have failed");
            }

        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            KMD0Rows.Add(new KMD0Row(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
        }

        private void DelRow_Click(object sender, RoutedEventArgs e)
        {
            if (KMD0Rows.IndexOf((KMD0Row)KMD0RowView.SelectedItem) != -1)
            {
                KMD0Rows.Remove((KMD0Row)KMD0RowView.SelectedItem);
                ICollectionView view = CollectionViewSource.GetDefaultView(KMD0RowView.ItemsSource);
                view.Refresh();
            }
            else
            {
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private void MoveRowUp_Click(object sender, RoutedEventArgs e)
        {
            int CurrentRowIndex = KMD0Rows.IndexOf((KMD0Row)KMD0RowView.SelectedItem);
            if (CurrentRowIndex > 0 && CurrentRowIndex <= KMD0Rows.Count - 1)
            {
                KMD0Rows.Move(CurrentRowIndex, CurrentRowIndex - 1);
            }
            else
            {
                System.Media.SystemSounds.Beep.Play();
            }
            ICollectionView view = CollectionViewSource.GetDefaultView(KMD0RowView.ItemsSource);
            view.Refresh();
        }

        private void MoveRowDown_Click(object sender, RoutedEventArgs e)
        {
            int CurrentRowIndex = KMD0Rows.IndexOf((KMD0Row)KMD0RowView.SelectedItem);
            
            if (CurrentRowIndex >= 0 && CurrentRowIndex < KMD0Rows.Count - 1)
            {
                KMD0Rows.Move(CurrentRowIndex, CurrentRowIndex + 1);
            }
            else
            {
                System.Media.SystemSounds.Beep.Play();
            }
            ICollectionView view = CollectionViewSource.GetDefaultView(KMD0RowView.ItemsSource);
            view.Refresh();
        }
    }

    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type TargetType, object parameter, CultureInfo culture)
        {
            ListViewItem item = (ListViewItem)value;
            ListView listView = ItemsControl.ItemsControlFromItemContainer(item) as ListView;
            int index = listView.ItemContainerGenerator.IndexFromContainer(item) - 1;
            return index.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

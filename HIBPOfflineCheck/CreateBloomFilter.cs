﻿using KeePass.App;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HIBPOfflineCheck
{
    public partial class CreateBloomFilter : Form
    {
        private HIBPOfflineCheckExt ext;

        private readonly CancellationTokenSource cancellationTokenSource;

        public CreateBloomFilter(HIBPOfflineCheckExt ext)
        {
            InitializeComponent();

            this.ext = ext;
            textBoxInput.Text = ext.Prov.PluginOptions.HIBPFileName;
            Icon = AppIcons.Default;
            cancellationTokenSource = new CancellationTokenSource();
        }

        private void buttonSelectInput_Click(object sender, EventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                string file = openDialog.FileName;
                textBoxInput.Text = file;
            }
        }

        private void buttonSelectOutput_Click(object sender, EventArgs e)
        {
            string defaultFileName = "HIBPBloomFilter.bin";

            var openDialog = new SaveFileDialog();
            openDialog.Filter = "All Files (*.*)|*.*";
            openDialog.FileName = defaultFileName;

            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                string file = openDialog.FileName;
                textBoxOutput.Text = file;
            }
        }

        private void CreateBloomFilter_FormClosed(object sender, FormClosedEventArgs e)
        {
            cancellationTokenSource.Cancel();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            cancellationTokenSource.Cancel();
            Close();
        }

        private void buttonCreate_Click(object sender, EventArgs e)
        {
            if (textBoxOutput.Text == string.Empty)
            {
                MessageBox.Show(
                    "Please specify an output file", 
                    "Output file not found",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var inputFile = textBoxInput.Text;
            if (!File.Exists(inputFile))
            {
                throw new FileNotFoundException();
            }

            Bloom();
        }

        private void BloomWorker(IProgress<int> progress, CancellationToken token)
        {
            progress.Report(1);

            var lineCount = 0;
            using (var reader = File.OpenText(textBoxInput.Text))
            {
                while (reader.ReadLine() != null)
                {
                    lineCount++;

                    if (lineCount % (1024 * 1024) == 0)
                    {
                        if (token.IsCancellationRequested)
                            return;
                    }
                }
            }

            //lineCount = 551509767;

            var currentLine = 0;
            var progressTick = lineCount / 100;

            BloomFilter bloomFilter = new BloomFilter(lineCount, 0.001F);

            progress.Report(5);

            var inputFile = textBoxInput.Text;

            using (var fs = File.OpenRead(inputFile))
            using (var sr = new StreamReader(fs))
            {
                while (sr.EndOfStream == false)
                {
                    var line = sr.ReadLine().Substring(0, 40);
                    bloomFilter.Add(line);
                    currentLine++;

                    if (currentLine % progressTick == 0)
                    {
                        progress.Report(5 + (int)(((double)currentLine) / lineCount * 90));

                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
            }

            bloomFilter.Save(textBoxOutput.Text);

            progress.Report(100);
        }

        private async void Bloom()
        {
            progressBar.Show();
            labelInfo.Text = "Generating filter...";

            var progress = new Progress<int>(percent =>
            {
                progressBar.Value = percent;

                var estimatedTime = 40;
                int timeRemaining = estimatedTime - percent * estimatedTime / 100;
                labelInfo.Text = "Time remaining: " + timeRemaining + (timeRemaining == 1 ? " minute" : " minutes");
            });

            CancellationToken token = cancellationTokenSource.Token;

            await Task.Run(() => BloomWorker(progress, token));

            labelInfo.Text = "Bloom filter successfully created!";
        }
    }
}

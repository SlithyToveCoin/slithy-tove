//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Recovery Words Dialog
//===============================================
namespace Slithy_Tove;

// Shows the words for a new wallet and makes the user confirm they saved them.
internal partial class RecoveryWordsDialog : Form
{
    private readonly string[] _words;
    private readonly int[] _positions;
    private readonly List<TextBox> _answers = [];
    private Panel? _challenge;
    private bool _checkingWords;
    private bool _confirmed;

    public RecoveryWordsDialog(string walletName, string recoveryWords)
    {
        InitializeComponent();
        _words = recoveryWords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (_words.Length == 0) throw new ArgumentException("Recovery words are missing.", nameof(recoveryWords));
        _positions = Enumerable.Range(0, _words.Length)
            .OrderBy(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue))
            .Take(Math.Min(3, _words.Length)).Order().ToArray();
        walletNameLabel.Text = walletName;
        recoveryWordsTextBox.Text = recoveryWords;
        continueButton.DialogResult = DialogResult.None;
        continueButton.Text = "Check my backup";
        continueButton.Click += Continue_Click;
    }

    private void copyButton_Click(object? sender, EventArgs e)
    {
        if (_checkingWords)
        {
            _checkingWords = false;
            _challenge!.Visible = false;
            recoveryWordsTextBox.Visible = true;
            savedCheckBox.Visible = true;
            copyButton.Text = "Copy words";
            headingLabel.Text = "Save these words";
            introLabel.Text = "Write these words down in order and store them somewhere private. They can restore this wallet.";
            warningLabel.Text = "Anyone with these words can spend your coins. Never share them with support.";
            continueButton.Text = "Check my backup";
            continueButton.Enabled = savedCheckBox.Checked;
            foreach (TextBox answer in _answers) answer.Clear();
            return;
        }
        Clipboard.SetText(recoveryWordsTextBox.Text);
        copyButton.Text = "Copied";
    }

    private void savedCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        continueButton.Enabled = !_checkingWords && savedCheckBox.Checked;
    }

    private void Continue_Click(object? sender, EventArgs e)
    {
        if (!_checkingWords)
        {
            if (!savedCheckBox.Checked) return;
            _checkingWords = true;
            recoveryWordsTextBox.Visible = false;
            savedCheckBox.Visible = false;
            headingLabel.Text = "Check your written backup";
            introLabel.Text = "Look at the copy you saved. Enter these words in their original positions.";
            copyButton.Text = "Show words again";
            continueButton.Text = "Finish";
            continueButton.Enabled = false;
            if (_challenge is null)
            {
                _challenge = new Panel { Bounds = recoveryWordsTextBox.Bounds };
                for (int i = 0; i < _positions.Length; i++)
                {
                    Label label = new() { Text = "Word " + (_positions[i] + 1), AutoSize = true, Location = new Point(0, i * 36 + 5) };
                    TextBox answer = new() { Location = new Point(105, i * 36), Width = 300, AccessibleName = label.Text, UseSystemPasswordChar = true };
                    answer.TextChanged += (_, _) => continueButton.Enabled = _checkingWords
                        ? _answers.All(box => !string.IsNullOrWhiteSpace(box.Text)) : savedCheckBox.Checked;
                    _answers.Add(answer);
                    _challenge.Controls.Add(label);
                    _challenge.Controls.Add(answer);
                }
                Controls.Add(_challenge);
            }
            _challenge.Visible = true;
            _challenge.BringToFront();
            _answers[0].Focus();
            return;
        }
        if (!_positions.Select((position, i) => _words[position].Equals(_answers[i].Text.Trim(), StringComparison.OrdinalIgnoreCase)).All(match => match))
        {
            warningLabel.Text = "Those words do not match. Check their positions on your written copy, or show the words again.";
            return;
        }
        _confirmed = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_confirmed)
        {
            e.Cancel = true;
            MessageBox.Show(this, "Write down your recovery words, then complete the word check before continuing.", "Save your recovery words");
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        recoveryWordsTextBox.Clear();
        foreach (TextBox answer in _answers) answer.Clear();
        Array.Clear(_words);
        base.OnFormClosed(e);
    }
}

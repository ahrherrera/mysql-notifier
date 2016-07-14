﻿// Copyright (c) 2013, 2016, Oracle and/or its affiliates. All rights reserved.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License as
// published by the Free Software Foundation; version 2 of the
// License.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
// 02110-1301  USA

using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MySql.Notifier.Classes;
using MySql.Notifier.Properties;
using MySQL.Utility.Classes;
using MySQL.Utility.Classes.MySQLWorkbench;
using MySQL.Utility.Forms;

namespace MySql.Notifier.Forms
{
  public partial class WindowsConnectionDialog : MachineAwareForm
  {
    #region Constants

    /// <summary>
    /// Regular expresion for a valid computer's IP address.
    /// </summary>
    private const string VALID_IP_REGEX = @"^((?<FirstToThird>2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(?<Last>2[0-4]\d|25[0-5]|[01]?\d\d?)$";

    /// <summary>
    /// Regular expression for a valid Host name.
    /// </summary>
    /// <remarks>
    /// Rules as per this link:
    /// https://en.wikipedia.org/wiki/Hostname
    /// </remarks>
    private const string VALID_HOSTNAME_REGEX = @"^" + VALID_HOSTNAME_PLAIN_REGEX + "$";

    /// <summary>
    /// Regular expression for a valid Host name.
    /// </summary>
    /// <remarks>
    /// Rules as per this link:
    /// https://en.wikipedia.org/wiki/Hostname
    /// </remarks>
    private const string VALID_HOSTNAME_PLAIN_REGEX = @"(?<FirstComponent>[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])(?<OtherComponentWithLeadingDot>\.([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]{0,61}[a-zA-Z0-9]))*";

    /// <summary>
    /// Regular expression
    /// </summary>
    /// <remarks>
    /// Rules as per these links:
    /// https://support.microsoft.com/en-us/kb/909264
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/aa380525(v=vs.85).aspx
    /// </remarks>
    private const string VALID_LOGON_NAME_REGEX = @"(?<LogonName>(?![\x20.]+$)([^\\@]{1,64}))";

    /// <summary>
    /// Regular expresion for a valid down-level logon name.
    /// </summary>
    /// <remarks>
    /// Rules as per these links:
    /// https://support.microsoft.com/en-us/kb/909264
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/aa380525(v=vs.85).aspx
    /// </remarks>
    private const string VALID_DOWN_LEVEL_LOGON_NAME_REGEX = @"^(?<NetBiosDomainName>(?![\x20.]+$)([^\\/:\*\?""<>|\.]{1,15})(?<Separator>\\))?" + VALID_LOGON_NAME_REGEX + "$";

    /// <summary>
    /// Regular expresion for a valid user principal name.
    /// </summary>
    /// <remarks>
    /// Rules as per these links:
    /// https://support.microsoft.com/en-us/kb/909264
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/aa380525(v=vs.85).aspx
    /// </remarks>
    private const string VALID_USER_PRINCIPAL_NAME_REGEX = @"^" + VALID_LOGON_NAME_REGEX + @"(?<UpnSuffix>(?<Separator>@)" + VALID_HOSTNAME_PLAIN_REGEX + ")?$";

    #endregion Constants

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsConnectionDialog"/> class.
    /// </summary>
    public WindowsConnectionDialog()
    {
      InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsConnectionDialog"/> class.
    /// </summary>
    /// <param name="machineslist">List of the machines already being added to monitor services.</param>
    /// <param name="currentMachine">Current machine for editing purposes.</param>
    public WindowsConnectionDialog(MachinesList machineslist, Machine currentMachine)
      : this()
    {
      Text = currentMachine == null ? Text : Resources.EditMachineText;
      errorToolTip.SetToolTip(hostErrorSign, Resources.HostToolTipText);
      errorToolTip.SetToolTip(userErrorSign, Resources.UserToolTipText);

      if (currentMachine != null)
      {
        NewMachine = currentMachine;
        HostTextBox.Text = currentMachine.Name;
        UserTextBox.Text = currentMachine.User;
        PasswordTextBox.Text = currentMachine.UnprotectedPassword;
        MachineAutoTestConnectionLabel.Visible = true;
        MachineAutoTestConnectionIntervalNumericUpDown.Visible = true;
        MachineAutoTestConnectionIntervalNumericUpDown.Value = NewMachine.AutoTestConnectionInterval;
        MachineAutoTestConnectionIntervalUOMComboBox.Visible = true;
        MachineAutoTestConnectionIntervalUOMComboBox.SelectedIndex = (int)NewMachine.AutoTestConnectionIntervalUnitOfMeasure;
        Height = 265;
        EditMode = true;
      }
      else
      {
        NewMachine = new Machine();
        MachineAutoTestConnectionLabel.Visible = false;
        MachineAutoTestConnectionIntervalNumericUpDown.Visible = false;
        MachineAutoTestConnectionIntervalUOMComboBox.Visible = false;
        Height = 235;
        EditMode = false;
      }

      if (machineslist == null)
      {
        return;
      }

      if (machineslist.Machines != null)
      {
        MachinesList = machineslist;
      }
    }

    public override sealed string Text
    {
      get { return base.Text; }
      set { base.Text = value; }
    }

    /// <summary>
    /// Gets a value indicating whether the dialog is in edit mode VS add mode.
    /// </summary>
    public bool EditMode { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user entries seem valid credentials to perform a connection test.
    /// </summary>
    public bool EntriesAreValid
    {
      get
      {
        return !(hostErrorSign.Visible || userErrorSign.Visible || string.IsNullOrEmpty(HostTextBox.Text) || string.IsNullOrEmpty(UserTextBox.Text) || string.IsNullOrEmpty(PasswordTextBox.Text));
      }
    }

    /// <summary>
    /// Handles the click event for both TestConnectionButton and DialogOKButton
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void Button_Click(object sender, EventArgs e)
    {
      Cursor = Cursors.WaitCursor;
      DialogOKButton.Enabled = false;
      NewMachine.Name = HostTextBox.Text.Trim();
      NewMachine.User = UserTextBox.Text.Trim();
      NewMachine.Password = MySqlSecurity.EncryptPassword(PasswordTextBox.Text);
      bool editMode = Text.Equals(Resources.EditMachineText);
      bool testConnection = sender.Equals(TestConnectionButton);
      bool connectionSuccessful = (testConnection || !editMode) && TestConnectionAndPermissionsSet(testConnection);

      if (testConnection && connectionSuccessful)
      {
        InfoDialog.ShowDialog(InfoDialogProperties.GetSuccessDialogProperties(Resources.ConnectionSuccessfulTitle, Resources.ConnectionSuccessfulMessage));
        DialogOKButton.Enabled = EntriesAreValid;
        DialogOKButton.Focus();
      }
      else if (!testConnection && (connectionSuccessful || editMode))
      {
        DialogOKButton.DialogResult = DialogResult = DialogResult.OK;
      }

      Cursor = Cursors.Default;
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="MachineAutoTestConnectionIntervalNumericUpDown"/> value changes.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void MachineAutoTestConnectionIntervalNumericUpDown_ValueChanged(object sender, EventArgs e)
    {
      NewMachine.AutoTestConnectionInterval = (uint)MachineAutoTestConnectionIntervalNumericUpDown.Value;
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="MachineAutoTestConnectionIntervalUOMComboBox"/> selected index changes.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void MachineAutoTestConnectionIntervalUOMComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
      NewMachine.AutoTestConnectionIntervalUnitOfMeasure = (TimeUtilities.IntervalUnitOfMeasure)MachineAutoTestConnectionIntervalUOMComboBox.SelectedIndex;
    }

    /// <summary>
    /// Performs a connection test to the remote host as well as validating the right permissions are set for the credentials provided by the user.
    /// </summary>
    /// <param name="forceTest">Indicates whether the test is performed regardless of the current status of the machine.</param>
    /// <returns>True if no problems were found during the test.</returns>
    private bool TestConnectionAndPermissionsSet(bool forceTest)
    {
      if (!EntriesAreValid)
      {
        return false;
      }

      if (forceTest || !NewMachine.IsOnline)
      {
        NewMachine.TestConnection(true, false);
      }

      return NewMachine.IsOnline;
    }

    /// <summary>
    /// Starts the timer to perform entry validation once the user has stopped writing long enough to letthe tick occurs.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void Textbox_TextChanged(object sender, EventArgs e)
    {
      timerTextChanged.Stop();
      timerTextChanged.Start();
    }

    /// <summary>
    /// Calls the ValidateEntries entries immediately.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void TextBox_Validating(object sender, System.ComponentModel.CancelEventArgs e)
    {
      timerTextChanged_Tick(timerTextChanged, EventArgs.Empty);
    }

    /// <summary>
    /// Performs entry validation when the user stopped writing for long enough to let the tick event occur.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void timerTextChanged_Tick(object sender, EventArgs e)
    {
      bool validate = timerTextChanged.Enabled;
      timerTextChanged.Stop();
      if (validate)
      {
        ValidateEntries();
      }
    }

    /// <summary>
    /// Validates user entries seem valid credentials on the textboxes  to perform a connection test.
    /// </summary>
    private void ValidateEntries()
    {
      // Validate Host name
    if (!string.IsNullOrEmpty(HostTextBox.Text))
      {
        bool validName;
        string hostname = HostTextBox.Text.Trim();
        if (hostname.ToLowerInvariant() == MySqlWorkbenchConnection.DEFAULT_HOSTNAME || hostname == MySqlWorkbenchConnection.LOCAL_IP || hostname == ".")
        {
          // Since we are attempting to add a remote computer, we deem the Host name as invalid if it resolves to a local machine.
          validName = false;
          InfoDialog.ShowDialog(InfoDialogProperties.GetErrorDialogProperties(Resources.CannotAddLocalhostTitle, Resources.CannotAddLocalhostMessage));
        }
        else if (!EditMode && MachinesList.HasMachineWithName(hostname))
        {
          // Host name already exists on the list of added remote machines.
          validName = false;
          InfoDialog.ShowDialog(InfoDialogProperties.GetErrorDialogProperties(Resources.MachineAlreadyExistTitle, Resources.MachineAlreadyExistMessage));
        }
        else
        {
          // Host name is also invalid if has non allowed characters or if is not a proper formated IP address.
          validName = Regex.IsMatch(hostname, VALID_HOSTNAME_REGEX) || Regex.IsMatch(hostname, VALID_IP_REGEX);
        }

        hostErrorSign.Visible = !validName;
      }

      // Username is invalid if if has non allowed characters.
      var userName = UserTextBox.Text;
      userErrorSign.Visible = !string.IsNullOrEmpty(userName)
                              && !Regex.IsMatch(userName, VALID_DOWN_LEVEL_LOGON_NAME_REGEX)
                              && !Regex.IsMatch(userName, VALID_USER_PRINCIPAL_NAME_REGEX);

      // Enable DialogOKButton if entries seem valid.
      DialogOKButton.Enabled = EntriesAreValid;
    }
  }
}
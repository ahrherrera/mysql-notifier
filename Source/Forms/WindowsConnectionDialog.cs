﻿//
// Copyright (c) 2013, Oracle and/or its affiliates. All rights reserved.
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
//

namespace MySql.Notifier
{
  using System;
  using System.Text.RegularExpressions;
  using System.Windows.Forms;
  using MySql.Notifier.Properties;
  using MySQL.Utility;
  using MySQL.Utility.Forms;

  public partial class WindowsConnectionDialog : MachineAwareForm
  {
    /// <summary>
    /// Regular Expresion for a valid Host/User name.
    /// </summary>
    private const string VALID_NAME_REGEX = @"^[\w\.\-_]{1,64}$";

    /// <summary>
    /// Regular Expresion for a valid computer's IP address.
    /// </summary>
    private const string VALID_IP_REGEX = @"^([01]?[\d][\d]?|2[0-4][\d]|25[0-5])(\.([01]?[\d][\d]?|2[0-4][\d]|25[0-5])){3}$";

    /// <summary>
    /// Returns true when the user entries seem valid credentials to perform a connection test.
    /// </summary>
    private bool EntriesAreValid
    {
      get
      {
        return !(hostErrorSign.Visible || userErrorSign.Visible || String.IsNullOrEmpty(HostTextBox.Text) || String.IsNullOrEmpty(UserTextBox.Text) || String.IsNullOrEmpty(PasswordTextBox.Text));
      }
    }

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
    public WindowsConnectionDialog(MachinesList machineslist, Machine CurrentMachine)
      : this()
    {
      if (CurrentMachine != null)
      {
        newMachine = CurrentMachine;
        Text = (string.IsNullOrEmpty(CurrentMachine.Name) || CurrentMachine.IsLocal) ? Text : "Edit Machine";
        HostTextBox.Text = (string.IsNullOrEmpty(CurrentMachine.Name) || CurrentMachine.IsLocal) ? String.Empty : CurrentMachine.Name;
        UserTextBox.Text = CurrentMachine.User ?? String.Empty;
        PasswordTextBox.Text = CurrentMachine.UnprotectedPassword ?? String.Empty;
      }
      if (machineslist != null)
      {
        if (machineslist.Machines != null)
        {
          machinesList = machineslist;
        }
      }
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
    /// Performs entry validation when the user stopped writing for long enough to let the tick event occur.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void timerTextChanged_Tick(object sender, EventArgs e)
    {
      ValidateEntries();
      timerTextChanged.Stop();
    }

    /// <summary>
    /// Calls the ValidateEntries entries immediately.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void TextBox_Validating(object sender, System.ComponentModel.CancelEventArgs e)
    {
      ValidateEntries();
    }

    /// <summary>
    /// Validates user entries seem valid credentials on the textboxes  to perform a connection test.
    /// </summary>
    private void ValidateEntries()
    {
      //// Validate Host name
      if (!string.IsNullOrEmpty(HostTextBox.Text))
      {
        string hostname = HostTextBox.Text.ToLowerInvariant();
        //// Host name is invalid if is a local machine
        bool hostNameIsNotRemote = (hostname == MySqlWorkbenchConnection.DEFAULT_HOSTNAME || hostname == MySqlWorkbenchConnection.LOCAL_IP || hostname == ".");
        //// Host name is also invalid if has non allowed characters or if is not a proper formated IP address.
        bool hostNameIsInvalid = hostNameIsNotRemote ? hostNameIsNotRemote : !(Regex.IsMatch(HostTextBox.Text, VALID_NAME_REGEX) || Regex.IsMatch(HostTextBox.Text, VALID_IP_REGEX));

        hostErrorSign.Visible = hostNameIsInvalid;

        if (hostNameIsNotRemote)
        {
          InfoDialog.ShowErrorDialog(Resources.CannotAddLocalhostTitle, Resources.CannotAddLocalhostMessage);
        }
      }

      //// Username is invalid if if has non allowed characters.
      userErrorSign.Visible = !string.IsNullOrEmpty(UserTextBox.Text) && !Regex.IsMatch(UserTextBox.Text, VALID_NAME_REGEX);

      //// Enable TestConnectionButton and DialogOKButton if entries seem valid.
      DialogOKButton.Enabled = TestConnectionButton.Enabled = EntriesAreValid;
    }

    /// <summary>
    /// Handles the click event for both TestConnectionButton and DialogOKButton
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void Button_Click(object sender, EventArgs e)
    {
      Cursor = Cursors.WaitCursor;
      DialogOKButton.Enabled = TestConnectionButton.Enabled = false;
      if (TestConnectionAndPermissionsSet(sender.Equals(TestConnectionButton)))
      {
        if (sender.Equals(TestConnectionButton))
        {
          InfoDialog.ShowSuccessDialog(Resources.ConnectionSuccessfulTitle, Resources.ConnectionSuccessfulMessage);
          DialogOKButton.Enabled = TestConnectionButton.Enabled = EntriesAreValid;
          DialogOKButton.Focus();
        }
        else
        {
          DialogOKButton.DialogResult = this.DialogResult = DialogResult.OK;
        }
      }
      Cursor = Cursors.Default;
    }

    /// <summary>
    /// Performs a connection test to the remote host as well as validating the right permissions are set for the credentials provided by the user.
    /// </summary>
    /// <param name="OnlyTest">Indicates whether the user is testing for connectivity or wanting to work with the provided credentials and close the dialog.</param>
    /// <returns>True if no problems were found during the test.</returns>
    private bool TestConnectionAndPermissionsSet(bool OnlyTest)
    {
      if (!EntriesAreValid)
      {
        return false;
      }

      newMachine = new Machine(HostTextBox.Text, UserTextBox.Text, PasswordTextBox.Text);
      newMachine.TestConnection(true, false);

      if (!OnlyTest && machinesList.GetMachineByHostName(newMachine.Name) != null)
      {
        if (InfoDialog.ShowYesNoDialog(InfoDialog.InfoType.Warning, Resources.MachineAlreadyExistTitle, Resources.MachineAlreadyExistMessage) == DialogResult.Yes)
        {
          newMachine = machinesList.OverwriteMachine(newMachine);
        }
        else
        {
          return false;
        }
      }

      return newMachine.IsOnline;
    }
  }
}
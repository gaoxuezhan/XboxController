using System;
using System.ComponentModel;
using System.Windows.Forms;
using XInputDotNetPure;
using System.Drawing;

namespace XInputReporter
{
    public partial class MainForm : Form
    {
        private ReporterState reporterState = new ReporterState();
        private Control[] controllerControls;
        private Control[] stickControls;
        private Point[] stickControlPositions;

        private bool isRunning;
        private String command;

        public MainForm()
        {
            InitializeComponent();

            controllerControls = new Control[] {
                picController1,
                picController2,
                picController3,
                picController4
            };
            stickControls = new Control[] {
                picStickLeft,
                picStickRight
            };
            stickControlPositions = new Point[] {
                picStickLeft.Location,
                picStickRight.Location
            };
        }

        private void pollingWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!e.Cancel)
            {
                if (reporterState.Poll())
                {
                    Invoke(new Action(UpdateState));
                }
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            MyTcpClient client = MyTcpClient.getInstance();
            //保证回调函数是在创建他的上下文执行（一般是UI线程）
            client.AsyncOperation = AsyncOperationManager.CreateOperation(null);
            /*
            client.Error += Client_Error; ;
            client.Receive += Client_Receive; ;
            client.Connected += Client_Connected;
            */
            client.Connect();

            comboDeadZone.Items.AddRange(Enum.GetNames(typeof(GamePadDeadZone)));
            comboDeadZone.SelectedItem = reporterState.DeadZone.ToString();

            pollingWorker.RunWorkerAsync();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            pollingWorker.CancelAsync();
        }

        private static string FormatFloat(float v)
        {
            return string.Format("{0:F3}", v);
        }

        private static void PositionStickControl(Control control, Point location, GamePadThumbSticks.StickValue value)
        {
            var deltaX = value.X * control.Width * 0.5f;
            var deltaY = value.Y * control.Height * -0.5f;

            control.Location = new Point(location.X + (int)deltaX, location.Y + (int)deltaY);
            control.Refresh();
        }

        private void UpdateState()
        {
            checkA.Checked = reporterState.LastActiveState.Buttons.A == XInputDotNetPure.ButtonState.Pressed;
            checkB.Checked = reporterState.LastActiveState.Buttons.B == XInputDotNetPure.ButtonState.Pressed;
            checkX.Checked = reporterState.LastActiveState.Buttons.X == XInputDotNetPure.ButtonState.Pressed;
            checkY.Checked = reporterState.LastActiveState.Buttons.Y == XInputDotNetPure.ButtonState.Pressed;
            checkStart.Checked = reporterState.LastActiveState.Buttons.Start == XInputDotNetPure.ButtonState.Pressed;
            checkBack.Checked = reporterState.LastActiveState.Buttons.Back == XInputDotNetPure.ButtonState.Pressed;
            checkGuide.Checked = reporterState.LastActiveState.Buttons.Guide == XInputDotNetPure.ButtonState.Pressed;
            checkStickLeft.Checked = reporterState.LastActiveState.Buttons.LeftStick == XInputDotNetPure.ButtonState.Pressed;
            checkStickRight.Checked = reporterState.LastActiveState.Buttons.RightStick == XInputDotNetPure.ButtonState.Pressed;
            checkShoulderLeft.Checked = reporterState.LastActiveState.Buttons.LeftShoulder == XInputDotNetPure.ButtonState.Pressed;
            checkShoulderRight.Checked = reporterState.LastActiveState.Buttons.RightShoulder == XInputDotNetPure.ButtonState.Pressed;

            checkDPadUp.Checked = reporterState.LastActiveState.DPad.Up == XInputDotNetPure.ButtonState.Pressed;
            checkDPadRight.Checked = reporterState.LastActiveState.DPad.Right == XInputDotNetPure.ButtonState.Pressed;
            checkDPadDown.Checked = reporterState.LastActiveState.DPad.Down == XInputDotNetPure.ButtonState.Pressed;
            checkDPadLeft.Checked = reporterState.LastActiveState.DPad.Left == XInputDotNetPure.ButtonState.Pressed;

            labelTriggerLeft.Text = FormatFloat(reporterState.LastActiveState.Triggers.Left);
            labelTriggerRight.Text = FormatFloat(reporterState.LastActiveState.Triggers.Right);

            labelStickLeftX.Text = FormatFloat(reporterState.LastActiveState.ThumbSticks.Left.X);
            labelStickLeftY.Text = FormatFloat(reporterState.LastActiveState.ThumbSticks.Left.Y);
            labelStickRightX.Text = FormatFloat(reporterState.LastActiveState.ThumbSticks.Right.X);
            labelStickRightY.Text = FormatFloat(reporterState.LastActiveState.ThumbSticks.Right.Y);

            if (reporterState.LastActiveState.Buttons.Start == XInputDotNetPure.ButtonState.Pressed)
            {
                timerStart.Start();
            }
            else
            {
                timerStart.Stop();
            }
            if (reporterState.LastActiveState.Buttons.Back == XInputDotNetPure.ButtonState.Pressed)
            {
                timerBack.Start();
            }
            else
            {
                timerBack.Stop();
            }

            for (int i = 0; i < 4; i++)
            {
                controllerControls[i].Visible = i == reporterState.LastActiveIndex && reporterState.LastActiveState.IsConnected;
            }

            PositionStickControl(stickControls[0], stickControlPositions[0], reporterState.LastActiveState.ThumbSticks.Left);
            PositionStickControl(stickControls[1], stickControlPositions[1], reporterState.LastActiveState.ThumbSticks.Right);

            //------------------------------------------
            String temp = "";

            if (checkA.Checked == false)
            {
                if (this.isRunning == true)
                {
                    this.isRunning = false;
                    // command stop
                    temp = "0";
                }
            }
            else {
                if (this.isRunning == false)
                {
                    this.isRunning = true;
                    // command go forward W1
                    temp = temp + "W1";
                }
                if (checkDPadUp.Checked == true)
                {
                    temp = temp + "W1";
                }
                if (checkDPadDown.Checked == true)
                {
                    // command go back Q2
                    temp = temp + "Q2";
                }
                if (checkDPadLeft.Checked == true)
                {
                    // command go left E4
                    temp = temp + "E4";
                }
                if (checkDPadRight.Checked == true)
                {
                    // command go right R3
                    temp = temp + "R3";
                }
            }
            if (temp != "")
            {
                temp = temp + "#";

                if (temp != this.command)
                {
                    MyTcpClient client = MyTcpClient.getInstance();
                    this.command = temp;
                    client.SendMsg(this.command);
                }
            }
        }

        private void checkLink_CheckedChanged(object sender, EventArgs e)
        {
            reporterState.LinkTriggersToVibration = checkLink.Checked;
        }

        private void comboDeadZone_SelectedValueChanged(object sender, EventArgs e)
        {
            reporterState.DeadZone = (GamePadDeadZone)Enum.Parse(typeof(GamePadDeadZone), comboDeadZone.SelectedItem as string);
        }

        private void timerStart_Tick(object sender, EventArgs e)
        {
            var deadZoneInt = (int)reporterState.DeadZone;
            deadZoneInt = (deadZoneInt + 1) % Enum.GetValues(typeof(GamePadDeadZone)).Length;
            comboDeadZone.SelectedItem = ((GamePadDeadZone)deadZoneInt).ToString();
            timerStart.Stop();
        }

        private void timerBack_Tick(object sender, EventArgs e)
        {
            checkLink.Checked = !checkLink.Checked;
            timerBack.Stop();
        }
    }
}

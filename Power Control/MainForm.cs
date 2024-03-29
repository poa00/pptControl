﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Power_Control
{
    public partial class MainForm : Form
    {
        public PXCMSession g_session;
        private volatile bool closing = false;
        public volatile bool stop = false;
        private Bitmap bitmap = null;

        private PXCMCapture.Handler _deviceChangeHandler;
        private PXCMCapture _capture;
        private Timer timer = new Timer();
        private string filename = null;
        public Dictionary<string, PXCMCapture.DeviceInfo> Devices { get; set; }

        private string gestureName = null;
        private bool _isInitGesturesFirstTime = false;

        private float pSize = 3.0f;

        private class Item
        {
            public string Name;
            public int Value;
            public Item(string name, int value)
            {
                Name = name; Value = value;
            }
            public override string ToString()
            {
                // Generates the text shown in the combo box
                return Name;
            }
        }


        public MainForm(PXCMSession session)
        {
            InitializeComponent();

            this.g_session = session;
            _deviceChangeHandler = new PXCMCapture.Handler { onDeviceListChanged = OnDeviceListChanged };
            PopulateDeviceMenu();
            cmbGesturesList.Enabled = false;

            FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
            Panel2.Paint += new PaintEventHandler(Panel_Paint);
            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = 2000;
            timer.Start();


        }

        void OnDeviceListChanged()
        {
            PopulateDeviceMenu();
            UpdateStatus("Device plugged/unplugged");
        }



        private delegate void UpdateGesturesToListDelegate(string gestureName, int index);
        public void UpdateGesturesToList(string gestureName, int index)
        {
            cmbGesturesList.Invoke(new UpdateGesturesToListDelegate(delegate (string name, int cmbIndex) { cmbGesturesList.Items.Add(new Item(name, cmbIndex)); }), new object[] { gestureName, index });
        }

        public void setInitGesturesFirstTime(bool isInit)
        {
            _isInitGesturesFirstTime = isInit;

        }

        private delegate void ResetGesturesListDelegate();
        public void resetGesturesList()
        {
            cmbGesturesList.Invoke(new ResetGesturesListDelegate(delegate () { cmbGesturesList.Text = ""; cmbGesturesList.Items.Clear(); cmbGesturesList.SelectedIndex = -1; cmbGesturesList.Enabled = false; cmbGesturesList.Size = new System.Drawing.Size(100, 20); }), new object[] { });
        }

        private delegate void UpdateGesturesListSizeDelegate();
        public void UpdateGesturesListSize()
        {
            cmbGesturesList.Invoke(new UpdateGesturesListSizeDelegate(delegate () { cmbGesturesList.Enabled = true; cmbGesturesList.Size = new System.Drawing.Size(100, 70); }), new object[] { });

        }

        public bool getInitGesturesFirstTime()
        {
            return _isInitGesturesFirstTime;
        }



        public string GetCheckedSmoother()
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals("Smoother")) continue;
                foreach (ToolStripMenuItem e in m.DropDownItems)
                {
                    if (e.Checked) return e.Text;
                }
            }
            return null;
        }

        public PXCMCapture.DeviceInfo GetDeviceFromFileMenu(string fileName)
        {
            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc();
            desc.group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR;
            desc.subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE;

            PXCMSession.ImplDesc desc1;
            PXCMCapture.DeviceInfo dinfo;
            PXCMSenseManager pp = PXCMSenseManager.CreateInstance();
            if (pp == null)
            {
                return null;
            }

            try
            {
                if (g_session.QueryImpl(desc, 0, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) throw null;

                if (pp.captureManager == null) throw null;

                if (g_session.QueryImpl(desc, 0, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) throw null;

                if (pp.captureManager.SetFileName(fileName, false) < pxcmStatus.PXCM_STATUS_NO_ERROR) throw null;

                if (pp.captureManager.LocateStreams() < pxcmStatus.PXCM_STATUS_NO_ERROR) throw null;

                if (pp.captureManager.device != null)
                    pp.captureManager.device.QueryDeviceInfo(out dinfo);
                else
                    throw null;
            }
            catch
            {
                pp.Dispose();
                return null;
            }


            pp.Close();
            pp.Dispose();

            StatusLabel.Text = "Ok";
            return dinfo;
        }

        private void PopulateDeviceMenu()
        {
            Devices = new Dictionary<string, PXCMCapture.DeviceInfo>();

            PXCMSession.ImplDesc desc = new PXCMSession.ImplDesc
            {
                @group = PXCMSession.ImplGroup.IMPL_GROUP_SENSOR,
                subgroup = PXCMSession.ImplSubgroup.IMPL_SUBGROUP_VIDEO_CAPTURE
            };
            ToolStripMenuItem sm = new ToolStripMenuItem("Device");
            for (int i = 0; ; i++)
            {
                PXCMSession.ImplDesc desc1;
                if (g_session.QueryImpl(desc, i, out desc1) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;

                if (g_session.CreateImpl<PXCMCapture>(desc1, out _capture) < pxcmStatus.PXCM_STATUS_NO_ERROR) continue;
                _capture.SubscribeToCaptureCallbacks(_deviceChangeHandler);
                for (int j = 0; ; j++)
                {
                    PXCMCapture.DeviceInfo dinfo;
                    if (_capture.QueryDeviceInfo(j, out dinfo) < pxcmStatus.PXCM_STATUS_NO_ERROR) break;
                    string name = dinfo.name;
                    if (Devices.ContainsKey(dinfo.name))
                    {
                        name += j;
                    }
                    Devices.Add(name, dinfo);
                    ToolStripMenuItem sm1 = new ToolStripMenuItem(dinfo.name, null, new EventHandler(Device_Item_Click));
                    sm.DropDownItems.Add(sm1);
                }
            }
            if (sm.DropDownItems.Count > 0)
            {
                var toolStripMenuItem = sm.DropDownItems[0] as ToolStripMenuItem;
                if (toolStripMenuItem != null)
                    toolStripMenuItem.Checked = true;
            }
            MainMenu.Items.RemoveAt(0);
            MainMenu.Items.Insert(0, sm);
        }




        private void RadioCheck(object sender, string name)
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals(name)) continue;
                foreach (ToolStripMenuItem e1 in m.DropDownItems)
                {
                    e1.Checked = (sender == e1);
                }
            }
        }

        private void Device_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Device");
        }

        private void Module_Item_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Module");
        }





        private void Start_Click(object sender, EventArgs e)
        {
            MainMenu.Enabled = false;
            Start.Enabled = false;
            Stop.Enabled = true;

            EnableTrackingMode(false);

            stop = false;
            System.Threading.Thread thread = new System.Threading.Thread(DoRecognition);
            thread.Start();
            System.Threading.Thread.Sleep(5);
        }

        delegate void DoRecognitionCompleted();
        private void DoRecognition()
        {
            HandsRecognition gr = new HandsRecognition(this);
            gr.SimplePipeline();
            this.Invoke(new DoRecognitionCompleted(
                delegate
                {
                    Start.Enabled = true;
                    Stop.Enabled = false;
                    EnableTrackingMode(true);
                    MainMenu.Enabled = true;
                    if (closing) Close();
                }
            ));
        }

        public string GetCheckedDevice()
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals("Device")) continue;
                foreach (ToolStripMenuItem e in m.DropDownItems)
                {
                    if (e.Checked) return e.Text;
                }
            }
            return null;
        }

        public PXCMCapture.DeviceInfo GetCheckedDeviceInfo()
        {
            foreach (ToolStripMenuItem m in MainMenu.Items)
            {
                if (!m.Text.Equals("Device")) continue;
                for (int i = 0; i < m.DropDownItems.Count; i++)
                {
                    if (((ToolStripMenuItem)m.DropDownItems[i]).Checked)
                        return Devices.ElementAt(i).Value;
                }
            }
            return new PXCMCapture.DeviceInfo();
        }


        public bool GetDepthState()
        {
            return Depth.Checked;
        }

        public bool GetLabelmapState()
        {
            return Labelmap.Checked;
        }

        public bool GetJointsState()
        {
            return Joints.Checked;
        }

        public bool GetSkeletonState()
        {
            return Skeleton.Checked;
        }

        public bool GetCursorState()
        {
            return Cursor.Checked;
        }

        public bool GetAdaptiveState()
        {
            return Adaptive.Checked;
        }

        public bool GetExtremitiesState()
        {
            return Extremities.Checked;
        }

        public bool GetCursorModeState()
        {
            return CursorMode.Checked;
        }

        public bool GetFullHandModeState()
        {
            return FullHandMode.Checked;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            stop = true;
            e.Cancel = Stop.Enabled;
            closing = true;
            if (_capture != null)
            {
                _capture.Dispose();
            }
        }

        private delegate void UpdateStatusDelegate(string status);
        public void UpdateStatus(string status)
        {
            Status2.Invoke(new UpdateStatusDelegate(delegate (string s) { StatusLabel.Text = s; }), new object[] { status });
        }

        private delegate void UpdateInfoDelegate(string status, Color color);
        public void UpdateInfo(string status, Color color)
        {
            infoTextBox.Invoke(new UpdateInfoDelegate(delegate (string s, Color c)
            {
                if (status == String.Empty)
                {
                    infoTextBox.Text = String.Empty;
                    return;
                }

                if (infoTextBox.TextLength > 1200)
                {
                    infoTextBox.Text = String.Empty;
                }

                infoTextBox.SelectionColor = c;

                infoTextBox.SelectedText = s;
                infoTextBox.SelectionColor = infoTextBox.ForeColor;

                infoTextBox.SelectionStart = infoTextBox.Text.Length;
                infoTextBox.ScrollToCaret();

            }), new object[] { status, color });
        }



        private delegate void UpdateFPSStatusDelegate(string status);
        public void UpdateFPSStatus(string status)
        {
            labelFPS.Invoke(new UpdateFPSStatusDelegate(delegate (string s) { labelFPS.Text = s; }), new object[] { status });
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            stop = true;
            EnableTrackingMode(true);
            resetGesturesList();
        }

        private delegate void UpdateEnableTrackingModeDelegate(bool flag);
        public void EnableTrackingMode(bool flag)
        {
            CursorMode.Invoke(new UpdateEnableTrackingModeDelegate(delegate (bool enable) { CursorMode.Enabled = enable; }), new object[] { flag });
            //ExtremitiesMode.Invoke(new UpdateEnableTrackingModeDelegate(delegate(bool enable) { ExtremitiesMode.Enabled = enable; }), new object[] { flag });
            FullHandMode.Invoke(new UpdateEnableTrackingModeDelegate(delegate (bool enable) { FullHandMode.Enabled = enable; }), new object[] { flag });
        }

        public void DisplayBitmap(Bitmap picture)
        {
            lock (this)
            {
                if (bitmap != null)
                    bitmap.Dispose();
                bitmap = new Bitmap(picture);
            }
        }

        private void Panel_Paint(object sender, PaintEventArgs e)
        {
            lock (this)
            {
                if (bitmap == null || bitmap.Width == 0 || bitmap.Height == 0) return;
                Bitmap bitmapNew = new Bitmap(bitmap);
                try
                {
                    if (Mirror.Checked)
                    {
                        bitmapNew.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    }

                    if (Scale2.Checked)
                    {
                        /* Keep the aspect ratio */
                        Rectangle rc = (sender as PictureBox).ClientRectangle;
                        float xscale = (float)rc.Width / (float)bitmap.Width;
                        float yscale = (float)rc.Height / (float)bitmap.Height;
                        float xyscale = (xscale < yscale) ? xscale : yscale;
                        int width = (int)(bitmap.Width * xyscale);
                        int height = (int)(bitmap.Height * xyscale);
                        rc.X = (rc.Width - width) / 2;
                        rc.Y = (rc.Height - height) / 2;
                        rc.Width = width;
                        rc.Height = height;
                        e.Graphics.DrawImage(bitmapNew, rc);
                    }
                    else
                    {
                        e.Graphics.DrawImageUnscaled(bitmapNew, 0, 0);
                    }
                }
                finally
                {
                    bitmapNew.Dispose();
                }
            }
        }

        public bool GetContourState()
        {
            return ContourCheckBox.Checked;
        }

        public void DisplayContour(PXCMPointI32[] contour, int blobNumber)
        {
            if (bitmap == null) return;
            lock (this)
            {
                Graphics g = Graphics.FromImage(bitmap);
                using (Pen contourColor = new Pen(Color.Blue, 3.0f))
                {
                    for (int i = 0; i < contour.Length; i++)
                    {
                        int baseX = (int)contour[i].x;
                        int baseY = (int)contour[i].y;

                        if (i + 1 < contour.Length)
                        {
                            int x = (int)contour[i + 1].x;
                            int y = (int)contour[i + 1].y;

                            g.DrawLine(contourColor, new Point(baseX, baseY), new Point(x, y));
                        }
                        else
                        {
                            int x = (int)contour[0].x;
                            int y = (int)contour[0].y;
                            g.DrawLine(contourColor, new Point(baseX, baseY), new Point(x, y));
                        }
                    }
                }
                g.Dispose();
            }
        }

        public void DisplayCursor(int numOfHands, Queue<PXCMPoint3DF32>[] cursorPoints, Queue<PXCMPoint3DF32>[] adaptivePoints, int[] cursorClick, PXCMCursorData.BodySideType[] handSideType)
        {
            if (bitmap == null) return;

            int scaleFactor = 1;
            Graphics g = Graphics.FromImage(bitmap);

            if (CursorMode.Checked)
            {
                Color color = Color.GreenYellow;
                Pen pen = new Pen(color, pSize);

                for (int i = 0; i < numOfHands; ++i)
                {
                    float sz = 8;
                    int blueColor = (handSideType[i] == PXCMCursorData.BodySideType.BODY_SIDE_LEFT)
                        ? 200
                        : (handSideType[i] == PXCMCursorData.BodySideType.BODY_SIDE_RIGHT) ? 100 : 0;

                    /// draw cursor trail
                    if (Cursor.Checked)
                    {
                        for (int j = 0; j < cursorPoints[i].Count; j++)
                        {
                            float greenPart = (float)((Math.Max(Math.Min(cursorPoints[i].ElementAt(j).z / scaleFactor, 0.7), 0.2) - 0.2) / 0.5);

                            pen.Color = Color.FromArgb(255, (int)(255 * (1 - greenPart)), (int)(255 * greenPart), blueColor);
                            pen.Width = pSize;
                            int x = (int)cursorPoints[i].ElementAt(j).x / scaleFactor;
                            int y = (int)cursorPoints[i].ElementAt(j).y / scaleFactor;
                            g.DrawEllipse(pen, x - sz / 2, y - sz / 2, sz, sz);
                        }
                    }

                    /// draw adaptive trail
                    if (Adaptive.Checked)
                    {
                        for (int j = 0; j < adaptivePoints[i].Count; j++)
                        {
                            float greenPart = (float)((Math.Max(Math.Min(adaptivePoints[i].ElementAt(j).z / scaleFactor, 0.7), 0.2) - 0.2) / 0.5);

                            pen.Color = Color.FromArgb(255, (int)(255 * (1 - greenPart)), (int)(255 * greenPart), blueColor);
                            pen.Width = pSize;
                            int x = (int)adaptivePoints[i].ElementAt(j).x / scaleFactor;
                            int y = (int)adaptivePoints[i].ElementAt(j).y / scaleFactor;
                            g.DrawEllipse(pen, x - sz / 2, y - sz / 2, sz, sz);
                        }
                    }

                    if (0 < cursorClick[i] && (Cursor.Checked || Adaptive.Checked))
                    {
                        color = Color.LightBlue;
                        pen = new Pen(color, 10.0f);
                        sz = 32;

                        int x = 0, y = 0;
                        if (Cursor.Checked && cursorPoints[i].Count() > 0)
                        {
                            x = (int)cursorPoints[i].ElementAt(cursorPoints[i].Count - 1).x / scaleFactor;
                            y = (int)cursorPoints[i].ElementAt(cursorPoints[i].Count - 1).y / scaleFactor;
                        }
                        else if (Adaptive.Checked && adaptivePoints[i].Count() > 0)
                        {
                            x = (int)adaptivePoints[i].ElementAt(adaptivePoints[i].Count - 1).x / scaleFactor;
                            y = (int)adaptivePoints[i].ElementAt(adaptivePoints[i].Count - 1).y / scaleFactor;
                        }
                        g.DrawEllipse(pen, x - sz / 2, y - sz / 2, sz, sz);
                    }
                }
                pen.Dispose();
            }
        }

        public void DisplayExtremities(int numOfHands, PXCMHandData.ExtremityData[][] extremitites = null)
        {
            if (bitmap == null) return;
            if (extremitites == null) return;

            int scaleFactor = 1;
            Graphics g = Graphics.FromImage(bitmap);

            float sz = 8;
            if (Labelmap.Checked)
                sz = 4;

            Pen pen = new Pen(Color.Red, 3.0f);
            for (int i = 0; i < numOfHands; ++i)
            {
                for (int j = 0; j < PXCMHandData.NUMBER_OF_EXTREMITIES; ++j)
                {
                    int x = (int)extremitites[i][j].pointImage.x / scaleFactor;
                    int y = (int)extremitites[i][j].pointImage.y / scaleFactor;
                    g.DrawEllipse(pen, x - sz / 2, y - sz / 2, sz, sz);
                }
            }
            pen.Dispose();
        }

        public void DisplayJoints(PXCMHandData.JointData[][] nodes, int numOfHands)
        {
            if (bitmap == null) return;
            if (nodes == null) return;

            if (Joints.Checked || Skeleton.Checked)
            {
                lock (this)
                {
                    int scaleFactor = 1;

                    Graphics g = Graphics.FromImage(bitmap);

                    using (Pen boneColor = new Pen(Color.DodgerBlue, 3.0f))
                    {
                        for (int i = 0; i < numOfHands; i++)
                        {
                            if (nodes[i][0] == null) continue;
                            int baseX = (int)nodes[i][0].positionImage.x / scaleFactor;
                            int baseY = (int)nodes[i][0].positionImage.y / scaleFactor;

                            int wristX = (int)nodes[i][0].positionImage.x / scaleFactor;
                            int wristY = (int)nodes[i][0].positionImage.y / scaleFactor;

                            if (Skeleton.Checked)
                            {
                                for (int j = 1; j < 22; j++)
                                {
                                    if (nodes[i][j] == null) continue;
                                    int x = (int)nodes[i][j].positionImage.x / scaleFactor;
                                    int y = (int)nodes[i][j].positionImage.y / scaleFactor;

                                    if (nodes[i][j].confidence <= 0) continue;

                                    if (j == 2 || j == 6 || j == 10 || j == 14 || j == 18)
                                    {

                                        baseX = wristX;
                                        baseY = wristY;
                                    }

                                    g.DrawLine(boneColor, new Point(baseX, baseY), new Point(x, y));
                                    baseX = x;
                                    baseY = y;
                                }
                            }

                            if (Joints.Checked)
                            {
                                using (
                                    Pen red = new Pen(Color.Red, 3.0f),
                                        black = new Pen(Color.Black, 3.0f),
                                        green = new Pen(Color.Green, 3.0f),
                                        blue = new Pen(Color.Blue, 3.0f),
                                        cyan = new Pen(Color.Cyan, 3.0f),
                                        yellow = new Pen(Color.Yellow, 3.0f),
                                        orange = new Pen(Color.Orange, 3.0f))
                                {
                                    Pen currnetPen = black;

                                    for (int j = 0; j < PXCMHandData.NUMBER_OF_JOINTS; j++)
                                    {
                                        float sz = 4;
                                        if (Labelmap.Checked)
                                            sz = 2;

                                        int x = (int)nodes[i][j].positionImage.x / scaleFactor;
                                        int y = (int)nodes[i][j].positionImage.y / scaleFactor;

                                        if (nodes[i][j].confidence <= 0) continue;

                                        //Wrist
                                        if (j == 0)
                                        {
                                            currnetPen = black;
                                        }

                                        //Center
                                        if (j == 1)
                                        {
                                            currnetPen = red;
                                            sz += 4;
                                        }

                                        //Thumb
                                        if (j == 2 || j == 3 || j == 4 || j == 5)
                                        {
                                            currnetPen = green;
                                        }
                                        //Index Finger
                                        if (j == 6 || j == 7 || j == 8 || j == 9)
                                        {
                                            currnetPen = blue;
                                        }
                                        //Finger
                                        if (j == 10 || j == 11 || j == 12 || j == 13)
                                        {
                                            currnetPen = yellow;
                                        }
                                        //Ring Finger
                                        if (j == 14 || j == 15 || j == 16 || j == 17)
                                        {
                                            currnetPen = cyan;
                                        }
                                        //Pinkey
                                        if (j == 18 || j == 19 || j == 20 || j == 21)
                                        {
                                            currnetPen = orange;
                                        }


                                        if (j == 5 || j == 9 || j == 13 || j == 17 || j == 21)
                                        {
                                            sz += 4;
                                        }

                                        g.DrawEllipse(currnetPen, x - sz / 2, y - sz / 2, sz, sz);
                                    }
                                }
                            }
                        }
                    }
                    g.Dispose();
                }
            }
        }



        private delegate void UpdatePanelDelegate();
        public void UpdatePanel()
        {

            Panel2.Invoke(new UpdatePanelDelegate(delegate ()
            {
                Panel2.Invalidate();
            }));

        }

        private void simpleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Pipeline");
        }

        private void advancedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RadioCheck(sender, "Pipeline");
        }

        private void timer_Tick(object sender, EventArgs e)
        {

        }

        private void Live_Click(object sender, EventArgs e)
        {
            Playback.Checked = Record.Checked = false;
            Live.Checked = true;
        }

        private void Playback_Click(object sender, EventArgs e)
        {
            Live.Checked = Record.Checked = false;
            Playback.Checked = true;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "RSSDK clip|*.rssdk|Old format clip|*.pcsdk|All files|*.*";
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;
                filename = (ofd.ShowDialog() == DialogResult.OK) ? ofd.FileName : null;
            }
        }

        public bool GetPlaybackState()
        {
            return Playback.Checked;
        }



        private void Record_Click(object sender, EventArgs e)
        {
            Live.Checked = Playback.Checked = false;
            Record.Checked = true;
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "RSSDK clip|*.rssdk|All Files|*.*";
            sfd.CheckPathExists = true;
            sfd.OverwritePrompt = true;
            sfd.AddExtension = true;
            filename = (sfd.ShowDialog() == DialogResult.OK) ? sfd.FileName : null;
        }

        public bool GetRecordState()
        {
            return Record.Checked;
        }

        public string GetFileName()
        {
            return filename;
        }

        private void cmbGesturesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbGesturesList.SelectedItem != null)
            {
                gestureName = cmbGesturesList.SelectedItem.ToString();
            }
            else
            {
                gestureName = string.Empty;
            }
        }

        public string GetGestureName()
        {
            return gestureName;
        }

        private void Depth_CheckedChanged(object sender, EventArgs e)
        {
            ContourCheckBox.Enabled = false;
        }

        private void Labelmap_CheckedChanged(object sender, EventArgs e)
        {
            ContourCheckBox.Enabled = true;
        }

        private void TrackingModeChanged(object sender, EventArgs e)
        {

            Labelmap.Enabled = true;

            Joints.Enabled = false;
            Joints.Checked = false;
            Skeleton.Enabled = false;
            Skeleton.Checked = false;
            Cursor.Enabled = false;
            Cursor.Checked = false;
            Adaptive.Enabled = false;
            Adaptive.Checked = false;
            Extremities.Enabled = false;
            Extremities.Checked = false;

            if (FullHandMode.Checked)
            {
                Labelmap.Enabled = true;
                Depth.Enabled = true;

                Joints.Enabled = true;
                Joints.Checked = true;
                Skeleton.Enabled = true;
                Skeleton.Checked = true;
                Extremities.Checked = false;
                Extremities.Enabled = true;
            }

            if (CursorMode.Checked)
            {
                Labelmap.Enabled = false;
                Labelmap.Checked = false;
                Depth.Enabled = false;

                Cursor.Checked = true;
                Cursor.Enabled = true;
                //Adaptive.Checked = true;
                Adaptive.Enabled = true;
            }
        }
    }
}

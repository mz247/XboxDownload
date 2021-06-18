﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace XboxDownload
{
    public partial class FormSniffer : Form
    {
        public FormSniffer()
        {
            InitializeComponent();

            float dpixRatio = Environment.OSVersion.Version.Major >= 10 ? CreateGraphics().DpiX / 96 : Program.Utility.DpiX / 96;
            if (dpixRatio > 1)
            {
                dgvGames.RowHeadersWidth = (int)(dgvGames.RowHeadersWidth * dpixRatio);
                foreach (DataGridViewColumn col in dgvGames.Columns)
                    col.Width = (int)(col.Width * dpixRatio);
            }
        }

        private void FormSniffer_Load(object sender, EventArgs e)
        {
            List<Games> games = new List<Games>
            {
                new Games("Kinect 体育竞技", "国语配音", "brhsml8030zn"),
                new Games("埃克朗守卫者", "", "bxb772lkh72z"),
                new Games("超神跑者", "", "c3pb0bp0g04l"),
                new Games("动物园大亨", "只能在国服显示中文", "brwjs8p512vf"),
                new Games("飞速骑行", "", "c5d7449r6sdr"),
                new Games("光之子", "国语配音", "bq9q620nc614"),
                new Games("雷曼传奇", "国语配音", "c26k4dvgr45b"),
                new Games("麦克斯：兄弟魔咒", "国语配音", "c0sfcf4pbrsz"),
                new Games("明星高尔夫", "国语配音", "bnq94hh98ztp"),
                new Games("摩托世界大奖赛2015", "", "bsh5fpmr3gd8"),
                new Games("桥", "", "c2p1cf27vvdf"),
                new Games("塞巴斯蒂安拉力赛：进化", "", "br5bk5pnzkvh"),
                new Games("水果忍者：体感版2", "", "btkfdf4dhwrz"),
                new Games("索尼克 力量", "", "bxk9z89s6rcx"),
                new Games("特技摩托：聚变", "", "bvcrsr6xsdw5"),
                new Games("体感功夫", "", "bvk1lrw59l64"),
                new Games("体感节奏战", "", "bs1c1bs3ss0v"),
                new Games("体感碰碰球", "", "c4j8pcxk5xlq"),
                new Games("体感碰碰球2", "", "c3ngpjhhwpw6"),
                new Games("体感章鱼", "", "bsfzlnb9r9rx"),
                new Games("型可塑", "国语配音", "c1b6dl0t68q5"),
                new Games("最终幻想15", "国服简中", "c45d79qvkztp"),

                new Games("真三国无双7 帝国", "国服简中", "未知"),  // /4/878a830c-f90a-490a-8fd5-66141c2b0a78/2d456c8a-fc28-4873-a536-bacff51bba25/1.2.1.0.d9ff4e56-dc51-451c-8937-ff4efcbcb376/SM7EMPCN_1.2.1.0_x64__zph8pnx224h38
                new Games("大蛇无双2 终极版", "国服简中", "未知")  // /5/9bc865b8-f36d-4e62-b1dc-b3eace95ebb7/8a657f9c-8fc6-4d95-8aa1-f052b12ae22d/1.1.5.0.e732cfd0-1f05-42af-8b5d-1a817ba06b76/OROCHI2UltimateCN_1.1.5.0_x64__zph8pnx224h38
            };
            //games.Sort((x, y) => string.Compare(x.Name, y.Name));

            List<DataGridViewRow> list = new List<DataGridViewRow>();
            foreach (var game in games)
            {
                DataGridViewRow dgvr = new DataGridViewRow();
                dgvr.CreateCells(dgvGames);
                dgvr.Resizable = DataGridViewTriState.False;
                dgvr.Cells[0].Value = game.Name;
                dgvr.Cells[1].Value = game.Note;
                dgvr.Cells[2].Value = game.ProductId.ToUpperInvariant();
                list.Add(dgvr);
            }
            if (list.Count >= 1)
            {
                dgvGames.Rows.AddRange(list.ToArray());
                dgvGames.ClearSelection();
            }
        }

        public string productid = null;

        private void DgvGames_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1 || dgvGames.SelectedRows.Count != 1) return;
            DataGridViewRow dgvr = dgvGames.SelectedRows[0];
            string productId = dgvr.Cells["Col_ProductId"].Value.ToString();
            if (Regex.IsMatch(productId, @"^[a-zA-Z0-9]{12}$"))
            {
                this.productid = dgvr.Cells["Col_ProductId"].Value.ToString();
                this.Close();
            }
        }

        private void DgvGames_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridView dgv = (DataGridView)sender;
            Rectangle rectangle = new Rectangle(e.RowBounds.Location.X, e.RowBounds.Location.Y, dgv.RowHeadersWidth - 1, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(), dgv.RowHeadersDefaultCellStyle.Font, rectangle, dgv.RowHeadersDefaultCellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://tieba.baidu.com/p/7302023199");
        }

        class Games
        {
            public String Name;
            public String Note;
            public String ProductId;

            public Games(String name, String note, String productid)
            {
                this.Name = name;
                this.Note = note;
                this.ProductId = productid;
            }
        }
    }
}

﻿namespace AquaMai.ErrorReport;

partial class CrashForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
        this.textLog = new System.Windows.Forms.TextBox();
        this.labelVersion = new System.Windows.Forms.Label();
        this.labelStatus = new System.Windows.Forms.Label();
        this.pictureBox1 = new System.Windows.Forms.PictureBox();
        this.label1 = new System.Windows.Forms.Label();
        this.tableLayoutPanel1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
        this.SuspendLayout();
        // 
        // tableLayoutPanel1
        // 
        this.tableLayoutPanel1.ColumnCount = 1;
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
        this.tableLayoutPanel1.Controls.Add(this.textLog, 0, 2);
        this.tableLayoutPanel1.Controls.Add(this.labelVersion, 0, 0);
        this.tableLayoutPanel1.Controls.Add(this.labelStatus, 0, 1);
        this.tableLayoutPanel1.Controls.Add(this.pictureBox1, 0, 3);
        this.tableLayoutPanel1.Controls.Add(this.label1, 0, 4);
        this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
        this.tableLayoutPanel1.Name = "tableLayoutPanel1";
        this.tableLayoutPanel1.RowCount = 5;
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 5F));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 5F));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 30F));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
        this.tableLayoutPanel1.Size = new System.Drawing.Size(702, 673);
        this.tableLayoutPanel1.TabIndex = 0;
        // 
        // textLog
        // 
        this.textLog.Dock = System.Windows.Forms.DockStyle.Fill;
        this.textLog.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.textLog.HideSelection = false;
        this.textLog.Location = new System.Drawing.Point(3, 69);
        this.textLog.Multiline = true;
        this.textLog.Name = "textLog";
        this.textLog.ReadOnly = true;
        this.textLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.textLog.Size = new System.Drawing.Size(696, 195);
        this.textLog.TabIndex = 0;
        // 
        // labelVersion
        // 
        this.labelVersion.Dock = System.Windows.Forms.DockStyle.Fill;
        this.labelVersion.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
        this.labelVersion.Location = new System.Drawing.Point(3, 0);
        this.labelVersion.Name = "labelVersion";
        this.labelVersion.Size = new System.Drawing.Size(696, 33);
        this.labelVersion.TabIndex = 1;
        this.labelVersion.Text = "label1";
        // 
        // labelStatus
        // 
        this.labelStatus.Dock = System.Windows.Forms.DockStyle.Fill;
        this.labelStatus.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
        this.labelStatus.Location = new System.Drawing.Point(3, 33);
        this.labelStatus.Name = "labelStatus";
        this.labelStatus.Size = new System.Drawing.Size(696, 33);
        this.labelStatus.TabIndex = 2;
        this.labelStatus.Text = "label2";
        // 
        // pictureBox1
        // 
        this.pictureBox1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
        this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
        this.pictureBox1.Location = new System.Drawing.Point(3, 270);
        this.pictureBox1.Name = "pictureBox1";
        this.pictureBox1.Size = new System.Drawing.Size(696, 330);
        this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.pictureBox1.TabIndex = 3;
        this.pictureBox1.TabStop = false;
        // 
        // label1
        // 
        this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
        this.label1.Font = new System.Drawing.Font("微软雅黑", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
        this.label1.Location = new System.Drawing.Point(3, 603);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(696, 70);
        this.label1.TabIndex = 4;
        this.label1.Text = "按任意键关闭此对话框\r\nPress any key to close this dialog";
        // 
        // CrashForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(702, 673);
        this.Controls.Add(this.tableLayoutPanel1);
        this.KeyPreview = true;
        this.Name = "CrashForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        this.Text = "AquaMai Crash Handler";
        this.TopMost = true;
        this.Load += new System.EventHandler(this.CrashForm_Load);
        this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.CrashForm_KeyDown);
        this.tableLayoutPanel1.ResumeLayout(false);
        this.tableLayoutPanel1.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
        this.ResumeLayout(false);
    }

    private System.Windows.Forms.Label label1;

    private System.Windows.Forms.PictureBox pictureBox1;

    private System.Windows.Forms.Label labelVersion;
    private System.Windows.Forms.Label labelStatus;

    private System.Windows.Forms.TextBox textLog;

    private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;

    #endregion
}

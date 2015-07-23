namespace TransferStation
{
    partial class MainForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.txtMsg = new System.Windows.Forms.TextBox();
            this.lstClient = new System.Windows.Forms.ListView();
            this.btnStartListen = new System.Windows.Forms.Button();
            this.btnStopListen = new System.Windows.Forms.Button();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.文件ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.工具ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.载入模块ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dgvStation = new System.Windows.Forms.DataGridView();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.发送命令ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.关闭连接ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.contextMenuStrip2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.卸载模块ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvStation)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.contextMenuStrip2.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtMsg
            // 
            this.txtMsg.Font = new System.Drawing.Font("黑体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtMsg.Location = new System.Drawing.Point(281, 282);
            this.txtMsg.Multiline = true;
            this.txtMsg.Name = "txtMsg";
            this.txtMsg.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtMsg.Size = new System.Drawing.Size(563, 185);
            this.txtMsg.TabIndex = 4;
            // 
            // lstClient
            // 
            this.lstClient.Font = new System.Drawing.Font("黑体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lstClient.Location = new System.Drawing.Point(281, 41);
            this.lstClient.Name = "lstClient";
            this.lstClient.Size = new System.Drawing.Size(563, 222);
            this.lstClient.TabIndex = 5;
            this.lstClient.UseCompatibleStateImageBehavior = false;
            this.lstClient.View = System.Windows.Forms.View.Details;
            this.lstClient.MouseClick += new System.Windows.Forms.MouseEventHandler(this.lstClient_MouseClick);
            // 
            // btnStartListen
            // 
            this.btnStartListen.Location = new System.Drawing.Point(12, 41);
            this.btnStartListen.Name = "btnStartListen";
            this.btnStartListen.Size = new System.Drawing.Size(104, 23);
            this.btnStartListen.TabIndex = 6;
            this.btnStartListen.Text = "开始监听";
            this.btnStartListen.UseVisualStyleBackColor = true;
            this.btnStartListen.Click += new System.EventHandler(this.btnStartListen_Click);
            // 
            // btnStopListen
            // 
            this.btnStopListen.Location = new System.Drawing.Point(142, 41);
            this.btnStopListen.Name = "btnStopListen";
            this.btnStopListen.Size = new System.Drawing.Size(109, 23);
            this.btnStopListen.TabIndex = 7;
            this.btnStopListen.Text = "关闭监听";
            this.btnStopListen.UseVisualStyleBackColor = true;
            this.btnStopListen.Click += new System.EventHandler(this.btnStopListen_Click);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.文件ToolStripMenuItem,
            this.工具ToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(856, 25);
            this.menuStrip1.TabIndex = 8;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // 文件ToolStripMenuItem
            // 
            this.文件ToolStripMenuItem.Name = "文件ToolStripMenuItem";
            this.文件ToolStripMenuItem.Size = new System.Drawing.Size(44, 21);
            this.文件ToolStripMenuItem.Text = "文件";
            // 
            // 工具ToolStripMenuItem
            // 
            this.工具ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.载入模块ToolStripMenuItem});
            this.工具ToolStripMenuItem.Name = "工具ToolStripMenuItem";
            this.工具ToolStripMenuItem.Size = new System.Drawing.Size(44, 21);
            this.工具ToolStripMenuItem.Text = "工具";
            // 
            // 载入模块ToolStripMenuItem
            // 
            this.载入模块ToolStripMenuItem.Name = "载入模块ToolStripMenuItem";
            this.载入模块ToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.载入模块ToolStripMenuItem.Text = "载入模块";
            this.载入模块ToolStripMenuItem.Click += new System.EventHandler(this.载入模块ToolStripMenuItem_Click);
            // 
            // dgvStation
            // 
            this.dgvStation.AllowUserToAddRows = false;
            this.dgvStation.AllowUserToDeleteRows = false;
            this.dgvStation.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvStation.GridColor = System.Drawing.Color.White;
            this.dgvStation.Location = new System.Drawing.Point(12, 82);
            this.dgvStation.Name = "dgvStation";
            this.dgvStation.RowTemplate.Height = 23;
            this.dgvStation.ShowRowErrors = false;
            this.dgvStation.Size = new System.Drawing.Size(263, 385);
            this.dgvStation.TabIndex = 9;
            this.dgvStation.CellMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dgvStation_CellMouseClick);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.发送命令ToolStripMenuItem,
            this.关闭连接ToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(119, 48);
            // 
            // 发送命令ToolStripMenuItem
            // 
            this.发送命令ToolStripMenuItem.Name = "发送命令ToolStripMenuItem";
            this.发送命令ToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
            this.发送命令ToolStripMenuItem.Text = "发送命令";
            this.发送命令ToolStripMenuItem.Click += new System.EventHandler(this.发送命令ToolStripMenuItem_Click);
            // 
            // 关闭连接ToolStripMenuItem
            // 
            this.关闭连接ToolStripMenuItem.Name = "关闭连接ToolStripMenuItem";
            this.关闭连接ToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
            this.关闭连接ToolStripMenuItem.Text = "关闭连接";
            this.关闭连接ToolStripMenuItem.Click += new System.EventHandler(this.关闭连接ToolStripMenuItem_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // contextMenuStrip2
            // 
            this.contextMenuStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.卸载模块ToolStripMenuItem});
            this.contextMenuStrip2.Name = "contextMenuStrip2";
            this.contextMenuStrip2.Size = new System.Drawing.Size(125, 26);
            // 
            // 卸载模块ToolStripMenuItem
            // 
            this.卸载模块ToolStripMenuItem.Name = "卸载模块ToolStripMenuItem";
            this.卸载模块ToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.卸载模块ToolStripMenuItem.Text = "卸载模块";
            this.卸载模块ToolStripMenuItem.Click += new System.EventHandler(this.卸载模块ToolStripMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(856, 479);
            this.Controls.Add(this.dgvStation);
            this.Controls.Add(this.lstClient);
            this.Controls.Add(this.btnStartListen);
            this.Controls.Add(this.btnStopListen);
            this.Controls.Add(this.txtMsg);
            this.Controls.Add(this.menuStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "MainForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainFrom_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvStation)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.contextMenuStrip2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtMsg;
        private System.Windows.Forms.ListView lstClient;
        private System.Windows.Forms.Button btnStartListen;
        private System.Windows.Forms.Button btnStopListen;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 文件ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 工具ToolStripMenuItem;
        private System.Windows.Forms.DataGridView dgvStation;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 发送命令ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 关闭连接ToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.ToolStripMenuItem 载入模块ToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip2;
        private System.Windows.Forms.ToolStripMenuItem 卸载模块ToolStripMenuItem;
    }
}


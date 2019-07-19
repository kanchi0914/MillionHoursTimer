namespace MHTimer.Components
{
    partial class NotifyIcon
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region コンポーネント デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NotifyIcon));
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.MHTimer = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItemExitApp = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemOpenApp = new System.Windows.Forms.ToolStripMenuItem();
            this.MHTimer.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.MHTimer;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "notifyIcon1";
            this.notifyIcon1.Visible = true;
            // 
            // MHTimer
            // 
            this.MHTimer.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemOpenApp,
            this.toolStripMenuItemExitApp});
            this.MHTimer.Name = "MHTimer";
            this.MHTimer.Size = new System.Drawing.Size(99, 48);
            this.MHTimer.Text = "終了";
            // 
            // toolStripMenuItemExitApp
            // 
            this.toolStripMenuItemExitApp.Name = "toolStripMenuItemExitApp";
            this.toolStripMenuItemExitApp.Size = new System.Drawing.Size(98, 22);
            this.toolStripMenuItemExitApp.Text = "終了";
            // 
            // toolStripMenuItemOpenApp
            // 
            this.toolStripMenuItemOpenApp.Name = "toolStripMenuItemOpenApp";
            this.toolStripMenuItemOpenApp.Size = new System.Drawing.Size(98, 22);
            this.toolStripMenuItemOpenApp.Text = "開く";
            this.MHTimer.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip MHTimer;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemOpenApp;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemExitApp;
    }
}

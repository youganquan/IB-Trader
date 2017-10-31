using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TWS
{
    public partial class MainForm : Form
    {
        StrategyPool strategyPool;
        public MainForm()
        {
            InitializeComponent();

            strategyPool = new StrategyPool();
        }
    }
}

using System.Drawing;
using System.Windows.Forms;

namespace FSGolfPL;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new OverlayForm());
    }
}

public sealed class OverlayForm : Form
{
    readonly Dictionary<string,string> D = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Carry"]="Lot", ["Roll"]="Toczenie", ["Total"]="Dystans całkowity", ["Lateral"]="Odchylenie boczne",
        ["Club Speed"]="Prędkość kija", ["Ball Speed"]="Prędkość piłki", ["Spin"]="Obroty", ["Spin Axis"]="Oś obrotu",
        ["Spin Loft"]="Loft dynamiczny", ["Smash"]="Współczynnik uderzenia", ["Launch V"]="Kąt startu pionowy",
        ["Launch H"]="Kąt startu poziomy", ["AOA"]="Kąt natarcia", ["Height"]="Wysokość", ["Flight Time"]="Czas lotu",
        ["Shot Type"]="Typ uderzenia", ["Ready"]="Gotowy", ["Connected"]="Połączony", ["Finish Session"]="Zakończ sesję",
        ["Radar Data"]="Dane radaru", ["Trajectory View"]="Widok trajektorii", ["Settings"]="Ustawienia",
        ["Full Swing"]="Pełny zamach", ["Putting Session"]="Sesja putting", ["Swing Training"]="Trening zamachu",
        ["Chipping Session"]="Sesja chipping", ["Review Session"]="Przegląd sesji", ["Play Mode"]="Tryb gry",
        ["Lateral Impact"]="Uderzenie boczne", ["Vertical Impact"]="Uderzenie pionowe", ["Face Impact"]="Miejsce uderzenia"
    };

    readonly FlowLayoutPanel panel = new() { AutoSize=true, WrapContents=false, FlowDirection=FlowDirection.LeftToRight, BackColor=Color.FromArgb(220,20,20,20), Padding=new Padding(8) };
    public OverlayForm()
    {
        Text = "FS Golf PL"; FormBorderStyle=FormBorderStyle.None; TopMost=true; ShowInTaskbar=true;
        BackColor=Color.Magenta; TransparencyKey=Color.Magenta; Opacity=0.92; StartPosition=FormStartPosition.Manual;
        Width=900; Height=55; Left=30; Top=30;
        var title = new Label { Text="FS Golf PL", ForeColor=Color.White, AutoSize=true, Font=new Font("Segoe UI",10,FontStyle.Bold), Margin=new Padding(0,7,15,0) };
        panel.Controls.Add(title);
        foreach (var kv in D.Take(10)) AddChip(kv.Key,kv.Value);
        Controls.Add(panel); panel.Dock=DockStyle.Fill;
        MouseDown += Drag; panel.MouseDown += Drag;
        var t=new System.Windows.Forms.Timer { Interval=1500 }; t.Tick += (_,_) => { RefreshTarget(); }; t.Start();
    }
    void AddChip(string en,string pl){ panel.Controls.Add(new Label{Text=$"{en} → {pl}",ForeColor=Color.White,BackColor=Color.FromArgb(180,35,35,35),AutoSize=true,Padding=new Padding(7,5,7,5),Margin=new Padding(3)}); }
    void Drag(object? s,MouseEventArgs e){ if(e.Button==MouseButtons.Left){ ReleaseCapture(); SendMessage(Handle,0xA1,2,0); } }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd,int msg,IntPtr w,IntPtr l);
    void RefreshTarget(){ /* first prototype: fixed translation bar; OCR/native UI Automation is the next module */ }
}

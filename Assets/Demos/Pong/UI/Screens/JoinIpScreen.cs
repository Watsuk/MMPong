using System.Net;
using TMPro;
using UnityEngine;

namespace MMPong.UI
{
    /// <summary>
    /// [5] Saisie de l'IP du salon + bouton Rejoindre, avec validation de format basique.
    /// IP valide → stockée dans la session → écran pseudo/équipe.
    /// </summary>
    public class JoinIpScreen : UIScreen
    {
        public override ScreenId Id => ScreenId.JoinIp;

        private TMP_InputField ipInput;
        private TextMeshProUGUI errorLabel;

        protected override void OnInit()
        {
            var root = UIFactory.CreateScreenRoot(transform, "Root");
            var col = UIFactory.CreateColumn(root.transform);

            UIFactory.CreateTitle(col, "Rejoindre un salon");
            UIFactory.CreateLabel(col, "Hint", "Adresse IP du salon", 22f);
            ipInput = UIFactory.CreateInputField(col, "IpInput", "127.0.0.1");

            errorLabel = UIFactory.CreateLabel(col, "Error", "", 20f);
            errorLabel.color = new Color(1f, 0.45f, 0.45f, 1f);

            UIFactory.CreateButton(col, "JoinButton", "Rejoindre", OnJoinClicked);

            AddBackButton(root.transform);
        }

        protected override void OnEnter()
        {
            errorLabel.text = "";
        }

        private void OnJoinClicked()
        {
            string ip = ipInput.text != null ? ipInput.text.Trim() : "";

            if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out _))
            {
                errorLabel.text = "Adresse IP invalide.";
                return;
            }

            Session.TargetIp = ip;
            Manager.Show(ScreenId.JoinSetup);
        }
    }
}

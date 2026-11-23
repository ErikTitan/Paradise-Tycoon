using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ParadiseTycoon.Ship
{
    /// <summary>Shows the incoming ship: guest count, requested services and accept/reject buttons.</summary>
    public sealed class ShipOfferPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text summary;
        [SerializeField] TMP_Text services;
        [SerializeField] Button accept;
        [SerializeField] Button reject;

        public Button AcceptButton => accept;
        public Button RejectButton => reject;
        public string SummaryText => summary.text;
        public string ServicesText => services.text;

        internal void Bind(Action acceptAction, Action rejectAction)
        {
            accept.onClick.AddListener(() => acceptAction());
            reject.onClick.AddListener(() => rejectAction());
        }

        internal void Show(ShipOffer offer, Func<string, string> displayName)
        {
            title.text = "Prichádza loď";
            summary.text = $"Hostia: {offer.GuestCount}   •   Požiadavky: {offer.RequestCount}";
            var text = new StringBuilder();
            foreach (var service in offer.RequestsPerService)
                text.Append(displayName(service.Key)).Append("  ×").Append(service.Value).Append('\n');
            services.text = text.ToString().TrimEnd();
            SetButtons(true);
            gameObject.SetActive(true);
        }

        internal void ShowUnavailable(string message)
        {
            title.text = "Žiadna loď";
            summary.text = message;
            services.text = "";
            SetButtons(false);
            gameObject.SetActive(true);
        }

        internal void Hide() => gameObject.SetActive(false);

        void SetButtons(bool visible)
        {
            accept.gameObject.SetActive(visible);
            reject.gameObject.SetActive(visible);
            accept.interactable = reject.interactable = visible;
        }
    }
}

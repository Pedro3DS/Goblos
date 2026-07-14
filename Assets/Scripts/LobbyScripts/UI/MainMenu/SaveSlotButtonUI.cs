using System;
using Project.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.MainMenu
{
    /// <summary>
    /// Um dos 3 botões de slot na tela "Criar Sala". Mostra se o slot está vazio
    /// ou já tem uma sala salva (nome + última vez jogado).
    /// </summary>
    public class SaveSlotButtonUI : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _subtitleText;
        [SerializeField] private GameObject _emptyBadge;

        private int _slotIndex;
        private Action<int> _onChosen;

        public void Bind(RoomSaveData data, Action<int> onChosen)
        {
            _slotIndex = data.SlotIndex;
            _onChosen = onChosen;

            if (data.HasData)
            {
                _titleText.text = data.RoomName;
                _subtitleText.text = FormatLastPlayed(data.LastPlayedAtUtc);
                if (_emptyBadge != null) _emptyBadge.SetActive(false);
            }
            else
            {
                _titleText.text = $"Slot {_slotIndex + 1}";
                _subtitleText.text = "Vazio - criar nova sala";
                if (_emptyBadge != null) _emptyBadge.SetActive(true);
            }

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _onChosen?.Invoke(_slotIndex));
        }

        private static string FormatLastPlayed(string isoUtc)
        {
            if (string.IsNullOrEmpty(isoUtc)) return string.Empty;

            if (DateTime.TryParse(isoUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var date))
            {
                return $"Última vez: {date.ToLocalTime():dd/MM/yyyy HH:mm}";
            }

            return string.Empty;
        }
    }
}

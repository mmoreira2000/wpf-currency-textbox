// Fork from mtusk CurrencyTextBox
// https://github.com/mtusk/wpf-currency-textbox
// 
// Fork 2016-2017 by Derek Tremblay (Abbaye) 
// derektremblay666@gmail.com

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace CurrencyTextBoxControl
{
    [ContentProperty("Number")]
    public class CurrencyTextBox : TextBox
    {
        #region Global variables / Event

        private static readonly char[] NumbersArray = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

        private readonly List<decimal> _undoList = new List<decimal>();
        private readonly List<decimal> _redoList = new List<decimal>();
        private Popup _popup;
        private Label _popupLabel;
        private decimal _numberBeforePopup;

        //Event
        public event EventHandler PopupClosed;
        public event EventHandler NumberChanged;
        #endregion Global variables

        #region Constructor
        static CurrencyTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CurrencyTextBox),
                new FrameworkPropertyMetadata(typeof(CurrencyTextBox)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Bind Text to Number with the specified StringFormat
            var textBinding = new Binding
            {
                Path = new PropertyPath("Number"),
                RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                StringFormat = StringFormat,
                ConverterCulture = Culture
            };

            BindingOperations.SetBinding(this, TextProperty, textBinding);

            // Disable copy/paste
            DataObject.AddCopyingHandler(this, CopyPasteEventHandler);
            DataObject.AddPastingHandler(this, CopyPasteEventHandler);

            //Events
            SetCaretPositionFromInputMode(this);

            //Disable contextmenu
            ContextMenu = null;
        }
        #endregion Constructor

        #region Dependency Properties

        public ExtendedInputCurrentModeEnum ExtendedInputCurrentMode
        {
            get => (ExtendedInputCurrentModeEnum)GetValue(ExtendedInputCurrentModeProperty);
            private set => SetValue(ExtendedInputCurrentModePropertyKey, value);
        }

        // Using a DependencyProperty as the backing store for ExtendedInputCurrentMode.  This enables animation, styling, binding, etc...
        private static readonly DependencyPropertyKey ExtendedInputCurrentModePropertyKey =
            DependencyProperty.RegisterReadOnly("ExtendedInputCurrentMode", typeof(ExtendedInputCurrentModeEnum), typeof(CurrencyTextBox), new PropertyMetadata(ExtendedInputCurrentModeEnum.Integer, ExtendedInputCurrentModeChanged));

        private static void ExtendedInputCurrentModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SetCaretPositionFromInputMode((CurrencyTextBox)d);
        }

        public static readonly DependencyProperty ExtendedInputCurrentModeProperty = ExtendedInputCurrentModePropertyKey.DependencyProperty;


        public InputTypeEnum InputType
        {
            get => (InputTypeEnum)GetValue(InputTypeProperty);
            set => SetValue(InputTypeProperty, value);
        }

        // Using a DependencyProperty as the backing store for InputType.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InputTypeProperty =
            DependencyProperty.Register("InputType", typeof(InputTypeEnum), typeof(CurrencyTextBox), new PropertyMetadata(InputTypeEnum.Extended, InputTypeChanged));

        private static void InputTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var currencyTextBox = (CurrencyTextBox)d;
            currencyTextBox.ExtendedInputCurrentMode = currencyTextBox.InputType == InputTypeEnum.Simplified && currencyTextBox.GetDigitCount() > 0 ?
                                                       ExtendedInputCurrentModeEnum.Decimal : ExtendedInputCurrentModeEnum.Integer;

            SetCaretPositionFromInputMode(currencyTextBox);
        }


        public static readonly DependencyProperty CultureProperty = DependencyProperty.Register(
            nameof(Culture), typeof(CultureInfo), typeof(CurrencyTextBox), new PropertyMetadata(CultureInfo.CurrentCulture, CulturePropertyChanged));

        private static void CulturePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var textBinding = new Binding
            {
                Path = new PropertyPath("Number"),
                RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                StringFormat = (string)d.GetValue(StringFormatProperty),
                ConverterCulture = (CultureInfo)e.NewValue
            };

            BindingOperations.SetBinding(d, TextProperty, textBinding);
        }

        public CultureInfo Culture
        {
            get => (CultureInfo)GetValue(CultureProperty);
            set => SetValue(CultureProperty, value);
        }

        public static readonly DependencyProperty NumberProperty = DependencyProperty.Register(
            nameof(Number), typeof(decimal), typeof(CurrencyTextBox),
            new FrameworkPropertyMetadata(0M, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                NumberPropertyChanged, NumberPropertyCoerceValue), NumberPropertyValidated);

        private static bool NumberPropertyValidated(object value) => value is decimal;

        private static object NumberPropertyCoerceValue(DependencyObject d, object baseValue)
        {
            if (d is CurrencyTextBox ctb)
            {
                var value = (decimal)baseValue;

                //Check maximum value
                if (value > ctb.MaximumValue && ctb.MaximumValue > 0)
                    return ctb.MaximumValue;

                if (value < ctb.MinimumValue && ctb.MinimumValue < 0)
                    return ctb.MinimumValue;

                return value;
            }

            return baseValue;
        }

        private static void NumberPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CurrencyTextBox ctb)
            {
                //Update IsNegative
                ctb.SetValue(IsNegativeProperty, ctb.Number < 0);

                //Launch event
                ctb.NumberChanged?.Invoke(ctb, new EventArgs());
            }
        }

        public decimal Number
        {
            get => (decimal)GetValue(NumberProperty);
            set => SetValue(NumberProperty, value);
        }

        public static readonly DependencyProperty IsNegativeProperty =
            DependencyProperty.Register(nameof(IsNegative), typeof(bool), typeof(CurrencyTextBox), new PropertyMetadata(false));

        public bool IsNegative => (bool)GetValue(IsNegativeProperty);

        public bool IsCalculPanelMode
        {
            get => (bool)GetValue(IsCalculPanelModeProperty);
            set => SetValue(IsCalculPanelModeProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsCalculPanelMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsCalculPanelModeProperty =
            DependencyProperty.Register(nameof(IsCalculPanelMode), typeof(bool), typeof(CurrencyTextBox), new PropertyMetadata(false));

        public bool CanShowAddPanel
        {
            get => (bool)GetValue(CanShowAddPanelProperty);
            set => SetValue(CanShowAddPanelProperty, value);
        }

        /// <summary>
        /// Set for enabling the calcul panel
        /// </summary>
        public static readonly DependencyProperty CanShowAddPanelProperty =
            DependencyProperty.Register(nameof(CanShowAddPanel), typeof(bool), typeof(CurrencyTextBox), new PropertyMetadata(false));

        public static readonly DependencyProperty MaximumValueProperty =
            DependencyProperty.Register(nameof(MaximumValue), typeof(decimal), typeof(CurrencyTextBox),
                new FrameworkPropertyMetadata(0M, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    MaximumValuePropertyChanged, MaximumCoerceValue), MaximumValidateValue);

        private static bool MaximumValidateValue(object value) => (decimal)value <= decimal.MaxValue / 2;

        private static object MaximumCoerceValue(DependencyObject d, object baseValue)
        {
            var ctb = d as CurrencyTextBox;

            if (ctb.MaximumValue > decimal.MaxValue / 2)
                return decimal.MaxValue / 2;

            return baseValue;
        }

        private static void MaximumValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctb = d as CurrencyTextBox;

            if (ctb.Number > (decimal)e.NewValue)
                ctb.Number = (decimal)e.NewValue;
        }

        public decimal MaximumValue
        {
            get => (decimal)GetValue(MaximumValueProperty);
            set => SetValue(MaximumValueProperty, value);
        }

        public decimal MinimumValue
        {
            get => (decimal)GetValue(MinimumValueProperty);
            set => SetValue(MinimumValueProperty, value);
        }

        public static readonly DependencyProperty MinimumValueProperty =
            DependencyProperty.Register(nameof(MinimumValue), typeof(decimal), typeof(CurrencyTextBox),
                new FrameworkPropertyMetadata(0M, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    MinimumValuePropertyChanged, MinimumCoerceValue), MinimumValidateValue);

        private static bool MinimumValidateValue(object value)
        {
            return (decimal)value >= decimal.MinValue / 2; //&& (decimal)value <= 0;
        }

        private static object MinimumCoerceValue(DependencyObject d, object baseValue)
        {
            var ctb = d as CurrencyTextBox;

            if (ctb.MinimumValue < decimal.MinValue / 2)
                return decimal.MinValue / 2;

            return baseValue;
        }

        private static void MinimumValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctb = d as CurrencyTextBox;

            if (ctb.Number < (decimal)e.NewValue)
                ctb.Number = (decimal)e.NewValue;
        }

        public static readonly DependencyProperty StringFormatProperty = DependencyProperty.Register(
            nameof(StringFormat), typeof(string), typeof(CurrencyTextBox),
            new FrameworkPropertyMetadata("C2", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                StringFormatPropertyChanged, StringFormatCoerceValue), StringFormatValidateValue);

        private static object StringFormatCoerceValue(DependencyObject d, object baseValue)
        {
            return ((string)baseValue).ToUpper();
        }

        /// <summary>
        /// Validate the StringFormat
        /// </summary>
        private static bool StringFormatValidateValue(object value)
        {
            var val = value.ToString().ToUpper();

            return val == "C0" || val == "C" || val == "C1" || val == "C2" || val == "C3" || val == "C4" || val == "C5" || val == "C6" ||
                val == "N0" || val == "N" || val == "N1" || val == "N2" || val == "N3" || val == "N4" || val == "N5" || val == "N6" ||
                val == "P0" || val == "P" || val == "P1" || val == "P2" || val == "P3" || val == "P4" || val == "P5" || val == "P6";
        }

        public string StringFormat
        {
            get => (string)GetValue(StringFormatProperty);
            set => SetValue(StringFormatProperty, value);
        }

        private static void StringFormatPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var currencyTextBox = (CurrencyTextBox)obj;
            currencyTextBox.ExtendedInputCurrentMode = currencyTextBox.InputType == InputTypeEnum.Simplified && currencyTextBox.GetDigitCount() > 0 ?
                                                       ExtendedInputCurrentModeEnum.Decimal : ExtendedInputCurrentModeEnum.Integer;

            // Update the Text binding with the new StringFormat
            var textBinding = new Binding
            {
                Path = new PropertyPath("Number"),
                RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                StringFormat = (string)e.NewValue,
                ConverterCulture = (CultureInfo)obj.GetValue(CultureProperty)
            };

            BindingOperations.SetBinding(obj, TextProperty, textBinding);
        }

        public int UpDownRepeat
        {
            get => (int)GetValue(UpDownRepeatProperty);
            set => SetValue(UpDownRepeatProperty, value);
        }

        /// <summary>
        /// Set the Up/down value when key repeated
        /// </summary>
        public static readonly DependencyProperty UpDownRepeatProperty =
            DependencyProperty.Register(nameof(UpDownRepeat), typeof(int), typeof(CurrencyTextBox), new PropertyMetadata(10));

        #endregion Dependency Properties

        #region Overrides

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            ExtendedInputCurrentMode = InputType == InputTypeEnum.Simplified && GetDigitCount() > 0 ?
                ExtendedInputCurrentModeEnum.Decimal : ExtendedInputCurrentModeEnum.Integer;

            base.OnGotFocus(e);
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            SetCaretPositionFromInputMode(this);
            base.OnTextChanged(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            SetInputModeFromCaretPosition(this);
            base.OnKeyDown(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            SetInputModeFromCaretPosition(this);
            base.OnMouseUp(e);
        }

        /// <summary>
        /// Action when is key pressed
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = true;
            if (IsReadOnly) return;

            if (KeyValidator.IsNumericKey(e.Key))
            {
                AddUndoInList(Number);
                InsertDigitOnRight(e.Key);
            }
            else if (KeyValidator.IsBackspaceKey(e.Key))
            {
                AddUndoInList(Number);
                RemoveRightMostDigit();
            }
            else if (KeyValidator.IsUpKey(e.Key))
            {
                AddUndoInList(Number);
                if (e.IsRepeat) AddValue(UpDownRepeat);
                else AddValue();
            }
            else if (KeyValidator.IsDownKey(e.Key))
            {
                AddUndoInList(Number);

                if (e.IsRepeat) SubtractValue(UpDownRepeat);
                else SubtractValue();
            }
            else if (KeyValidator.IsLeftKey(e.Key) &&
                     InputType != InputTypeEnum.Simplified)
            {
                ExtendedInputCurrentMode = ExtendedInputCurrentModeEnum.Integer;
                SetCaretPositionFromInputMode(this);
            }
            else if (KeyValidator.IsRightKey(e.Key) &&
                     InputType != InputTypeEnum.Simplified &&
                     ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Integer &&
                     GetDigitCount() > 0)
            {
                ExtendedInputCurrentMode = ExtendedInputCurrentModeEnum.Decimal;
                SetCaretPositionFromInputMode(this);
            }
            else if (KeyValidator.IsPointKey(e.Key) &&
                     InputType != InputTypeEnum.Simplified &&
                     ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Integer &&
                     GetDigitCount() > 0)
            {
                ExtendedInputCurrentMode = ExtendedInputCurrentModeEnum.Decimal;
                SetCaretPositionFromInputMode(this);
            }
            else if (KeyValidator.IsCtrlZKey(e.Key))
            {
                Undo();
            }
            else if (KeyValidator.IsCtrlYKey(e.Key))
            {
                Redo();
            }
            else if (KeyValidator.IsPlusKey(e.Key))
            {
                e.Handled = false;

                if (!IsCalculPanelMode)
                {
                    AddUndoInList(Number);
                    ShowAddPopup();
                }
                else
                {
                    var popUpNumber = this;
                    Number = _numberBeforePopup + popUpNumber.Number;
                    _numberBeforePopup = _numberBeforePopup + popUpNumber.Number;
                    popUpNumber.Number = 0;
                }
            }
            else if (KeyValidator.IsEnterKey(e.Key) && IsCalculPanelMode)
            {
                ((Popup)((Grid)Parent).Parent).IsOpen = false;

                if (PopupClosed != null)
                {
                    PopupClosed(this, new EventArgs());
                }
                else
                {
                    e.Handled = false;
                }
            }
            else if (KeyValidator.IsDeleteKey(e.Key))
            {
                AddUndoInList(Number);
                Clear();
            }
            else if (KeyValidator.IsSubstractKey(e.Key))
            {
                AddUndoInList(Number);
                InvertValue();
            }
            else if (KeyValidator.IsIgnoredKey(e.Key))
            {
                e.Handled = false;
            }
            else if (KeyValidator.IsCtrlCKey(e.Key))
            {
                CopyToClipBoard();
            }
            else if (KeyValidator.IsCtrlVKey(e.Key))
            {
                AddUndoInList(Number);
                PasteFromClipBoard();
            }
            base.OnPreviewKeyDown(e);
        }

        // cancel copy and paste
        private void CopyPasteEventHandler(object sender, DataObjectEventArgs e) => e.CancelCommand();

        #endregion

        #region Private Methods       
        private static void SetCaretPositionFromInputMode(CurrencyTextBox sender)
        {
            if (sender.InputType == InputTypeEnum.Simplified) sender.CaretIndex = sender.Text.LastIndexOfAny(NumbersArray) + 1;
            else
            {
                if (sender.ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Integer)
                {
                    string decimalSeparator = GetDecimalSeparator(sender);
                    var sepIndex = sender.Text.LastIndexOf(decimalSeparator, StringComparison.OrdinalIgnoreCase);
                    if (sepIndex < 0) sepIndex = sender.Text.LastIndexOfAny(NumbersArray) + 1;
                    sender.CaretIndex = sepIndex;
                }
                else sender.CaretIndex = sender.Text.LastIndexOfAny(NumbersArray) + 1;
            }
        }

        private static void SetInputModeFromCaretPosition(CurrencyTextBox textBox)
        {
            if (textBox.InputType == InputTypeEnum.Simplified)
            {
                textBox.ExtendedInputCurrentMode = textBox.GetDigitCount() > 0 ?
                                                   ExtendedInputCurrentModeEnum.Decimal :
                                                   ExtendedInputCurrentModeEnum.Integer;
            }
            else
            {
                string decimalSeparator = GetDecimalSeparator(textBox);
                int indexDecimal = textBox.Text.LastIndexOf(decimalSeparator, StringComparison.OrdinalIgnoreCase);

                if (indexDecimal < 0 || indexDecimal >= textBox.CaretIndex) textBox.ExtendedInputCurrentMode = ExtendedInputCurrentModeEnum.Integer;
                else textBox.ExtendedInputCurrentMode = ExtendedInputCurrentModeEnum.Decimal;
            }
            SetCaretPositionFromInputMode(textBox);
        }

        private void Ctb_NumberChanged(object sender, EventArgs e)
        {
            var ctb = sender as CurrencyTextBox;

            Number = _numberBeforePopup + ctb.Number;

            _popupLabel.Content = ctb.Number >= 0 ? "+" : "-";
        }

        private static string GetDecimalSeparator(CurrencyTextBox control)
        {
            if (string.IsNullOrEmpty(control.StringFormat) || !control.StringFormat.Left(1).Equals("C", StringComparison.OrdinalIgnoreCase))
                return control.Culture.NumberFormat.NumberDecimalSeparator;

            return control.Culture.NumberFormat.CurrencyDecimalSeparator;
        }

        /// <summary>
        /// Insert number from key
        /// </summary>
        private void InsertDigitOnRight(Key key)
        {
            //Max length fix
            if (MaxLength != 0 && Number.ToString(Culture).Length > MaxLength) return;
            if (!KeyValidator.IsNumericKey(key)) return;

            try
            {
                decimal num = Number;
                if (IsPercent()) num *= 100;

                var parts = GetNumberParts(num);
                string stringNumber;

                if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Decimal &&
                    (InputType == InputTypeEnum.Extended || InputType == InputTypeEnum.Extended))
                {
                    stringNumber = parts.IntegerPart.ToString("D") +
                                   parts.DecimalPart.ToString("D").PadLeft(parts.DigitCount, '0') +
                                   GetDigitFromKey(key);

                    stringNumber = stringNumber.Left(stringNumber.Length - parts.DigitCount) +
                                   Culture.NumberFormat.NumberDecimalSeparator +
                                   stringNumber.Right(parts.DigitCount);
                }
                else if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Integer)
                {
                    stringNumber = parts.IntegerPart.ToString("D") + GetDigitFromKey(key); ;
                    stringNumber = stringNumber +
                                   Culture.NumberFormat.NumberDecimalSeparator +
                                   parts.DecimalPart.ToString("D").PadLeft(parts.DigitCount, '0');
                }
                else
                {
                    stringNumber = parts.DecimalPart.ToString("D") + GetDigitFromKey(key);
                    stringNumber = parts.IntegerPart.ToString("D") +
                                   Culture.NumberFormat.NumberDecimalSeparator +
                                   stringNumber.PadLeft(parts.DigitCount, '0').Right(parts.DigitCount);
                }

                var dec = decimal.Parse(stringNumber, Culture);
                if (IsPercent()) dec /= 100;
                if (parts.IsNegative) dec *= -1;
                Number = dec;
            }
            catch (OverflowException)
            {
                Number = Number < 0 ? decimal.MinValue : decimal.MaxValue;
            }
        }

        /// <summary>
        /// Delete the right digit of number property
        /// </summary>
        private void RemoveRightMostDigit()
        {
            try
            {
                decimal num = Number;
                if (IsPercent()) num *= 100;

                var parts = GetNumberParts(num);
                string stringNumber = string.Empty;

                if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Decimal &&
                    (InputType == InputTypeEnum.Extended || InputType == InputTypeEnum.Extended))
                {
                    stringNumber = parts.IntegerPart.ToString("D") + parts.DecimalPart.ToString("D").PadLeft(parts.DigitCount, '0');
                    stringNumber = stringNumber.Left(stringNumber.Length - 1).PadLeft(parts.DigitCount + 1, '0');

                    stringNumber = stringNumber.Left(stringNumber.Length - parts.DigitCount) +
                                   Culture.NumberFormat.NumberDecimalSeparator +
                                   stringNumber.Right(parts.DigitCount);
                }
                else if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Integer)
                {
                    stringNumber = parts.IntegerPart.ToString("D");
                    stringNumber = stringNumber.Left(stringNumber.Length - 1);
                    if (string.IsNullOrEmpty(stringNumber)) stringNumber = "0";

                    stringNumber = stringNumber +
                                   Culture.NumberFormat.NumberDecimalSeparator +
                                   parts.DecimalPart.ToString("D").PadLeft(parts.DigitCount, '0');

                }
                else
                {
                    stringNumber = parts.DecimalPart.ToString("D").PadLeft(parts.DigitCount, '0');
                    stringNumber = stringNumber.Left(stringNumber.Length - 1).PadLeft(parts.DigitCount, '0');

                    stringNumber = parts.IntegerPart.ToString("D") +
                                   Culture.NumberFormat.NumberDecimalSeparator +
                                   stringNumber;
                }



                var dec = decimal.Parse(stringNumber, Culture);
                if (IsPercent()) dec /= 100;
                if (parts.IsNegative) dec *= -1;
                Number = dec;
            }
            catch
            {
                Clear();
            }
        }

        private void AddValue(int amount = 1)
        {
            try
            {
                decimal num = Number;
                if (IsPercent()) num *= 100;

                var parts = GetNumberParts(num);

                if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Decimal &&
                    (InputType == InputTypeEnum.Extended || InputType == InputTypeEnum.Extended))
                {
                    var strValue = $"0{parts.DecimalSeparator}{amount.ToString(Culture).PadLeft(parts.DigitCount, '0')}";
                    parts = GetNumberParts(num + decimal.Parse(strValue, Culture));
                }
                else if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Decimal)
                {
                    int maxDecimalValue = int.Parse(new string('9', parts.DigitCount), Culture);
                    if (parts.IsNegative) parts.DecimalPart -= amount;
                    else parts.DecimalPart += amount;

                    if (parts.DecimalPart > maxDecimalValue) parts.DecimalPart = maxDecimalValue;
                    else if (parts.DecimalPart < 0) parts.DecimalPart = 0;
                }
                else if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Integer)
                {
                    if (parts.IsNegative) parts.IntegerPart -= amount;
                    else parts.IntegerPart += amount;

                    if (parts.IntegerPart < 0)
                    {
                        parts.IsNegative = false;
                        parts.IntegerPart = Math.Abs(parts.IntegerPart);
                    }
                }

                string stringNumber = parts.IntegerPart.ToString("D") +
                                      Culture.NumberFormat.NumberDecimalSeparator +
                                      parts.DecimalPart.ToString("D").PadLeft(parts.DigitCount, '0');

                var dec = Math.Round(decimal.Parse(stringNumber, Culture), parts.DigitCount);
                if (IsPercent()) dec /= 100;
                if (parts.IsNegative) dec *= -1;
                Number = dec;
            }
            catch (OverflowException)
            {
                Number = Number < 0 ? decimal.MinValue : decimal.MaxValue;
            }
        }

        private void SubtractValue(int amount = 1)
        {
            try
            {
                decimal num = Number;
                if (IsPercent()) num *= 100;

                var parts = GetNumberParts(num);

                if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Decimal &&
                    (InputType == InputTypeEnum.Extended || InputType == InputTypeEnum.Extended))
                {
                    var strValue = $"0{parts.DecimalSeparator}{amount.ToString(Culture).PadLeft(parts.DigitCount, '0')}";
                    parts = GetNumberParts(num - decimal.Parse(strValue, Culture));
                }
                else if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Decimal)
                {
                    int maxDecimalValue = int.Parse(new string('9', parts.DigitCount), Culture);
                    if (parts.IsNegative) parts.DecimalPart += amount;
                    else parts.DecimalPart -= amount;

                    if (parts.DecimalPart > maxDecimalValue) parts.DecimalPart = maxDecimalValue;
                    else if (parts.DecimalPart < 0) parts.DecimalPart = 0;
                }
                else if (ExtendedInputCurrentMode == ExtendedInputCurrentModeEnum.Integer)
                {
                    if (parts.IsNegative) parts.IntegerPart += amount;
                    else parts.IntegerPart -= amount;

                    if (parts.IntegerPart < 0)
                    {
                        parts.IsNegative = true;
                        parts.IntegerPart = Math.Abs(parts.IntegerPart);
                    }
                }

                string stringNumber = parts.IntegerPart.ToString("D") +
                                      Culture.NumberFormat.NumberDecimalSeparator +
                                      parts.DecimalPart.ToString("D").PadLeft(parts.DigitCount, '0');

                var dec = Math.Round(decimal.Parse(stringNumber, Culture), parts.DigitCount);
                if (IsPercent()) dec /= 100;
                if (parts.IsNegative) dec *= -1;
                Number = dec;
            }
            catch (OverflowException)
            {
                Number = Number < 0 ? decimal.MinValue : decimal.MaxValue;
            }
        }

        private NumberParts GetNumberParts(decimal value)
        {
            var resp = new NumberParts();
            resp.IsNegative = value < 0;
            resp.DigitCount = GetDigitCount();
            resp.DecimalSeparator = StringFormat.StartsWith("C", StringComparison.OrdinalIgnoreCase)
                                    ? Culture.NumberFormat.CurrencyDecimalSeparator
                                    : Culture.NumberFormat.NumberDecimalSeparator;

            string numberString = Math.Abs(value).ToString("0.##############", Culture);
            string[] numParts = numberString.Split(Culture.NumberFormat.NumberDecimalSeparator.ToCharArray());
            if (numParts.Length == 1)
            {
                resp.IntegerPart = long.Parse(numParts[0]);
            }
            else if (numParts.Length == 2)
            {
                resp.IntegerPart = long.Parse(numParts[0]);
                resp.DecimalPart = long.Parse(numParts[1].PadRight(resp.DigitCount, '0'));
            }
            else
            {
                throw new InvalidDataException($"Error parsing number '{numParts}'. This number returned {numParts.Length} when the maximum allowed was 2");
            }

            return resp;
        }

        /// <summary>
        /// Get the digit from key
        /// </summary>        
        private static decimal GetDigitFromKey(Key key)
        {
            switch (key)
            {
                case Key.D0:
                case Key.NumPad0: return 0M;
                case Key.D1:
                case Key.NumPad1: return 1M;
                case Key.D2:
                case Key.NumPad2: return 2M;
                case Key.D3:
                case Key.NumPad3: return 3M;
                case Key.D4:
                case Key.NumPad4: return 4M;
                case Key.D5:
                case Key.NumPad5: return 5M;
                case Key.D6:
                case Key.NumPad6: return 6M;
                case Key.D7:
                case Key.NumPad7: return 7M;
                case Key.D8:
                case Key.NumPad8: return 8M;
                case Key.D9:
                case Key.NumPad9: return 9M;
                default: throw new ArgumentOutOfRangeException($"Invalid key: {key}");
            }
        }

        /// <summary>
        /// Get the number of digit .
        /// </summary>
        /// <param name="format"></param>
        private int GetDigitCount(string format = null)
        {
            if (string.IsNullOrEmpty(format)) format = StringFormat;
            if (string.IsNullOrEmpty(format)) return 1;
            if (string.Equals("N", format, StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals("C", format, StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals("P", format, StringComparison.OrdinalIgnoreCase)) return 2;

            var lastDigit = format.Right(1);
            if (int.TryParse(lastDigit, NumberStyles.Integer, Culture, out var resp)) return resp;

            return 1;
        }

        private bool IsPercent()
        {
            if (string.IsNullOrEmpty(StringFormat)) return false;
            return StringFormat.StartsWith("P", StringComparison.OrdinalIgnoreCase);
        }

        #endregion Privates methodes

        #region Undo/Redo 

        /// <summary>
        /// Add undo to the list
        /// </summary>
        private void AddUndoInList(decimal number, bool clearRedo = true)
        {
            //Clear first item when undolimit is reach
            if (_undoList.Count == UndoLimit)
                _undoList.RemoveRange(0, 1);

            //Add item to undo list
            _undoList.Add(number);

            //Clear redo when needed
            if (clearRedo)
                _redoList.Clear();
        }

        /// <summary>
        /// Undo the to the previous value
        /// </summary>
        public new void Undo()
        {
            if (CanUndo())
            {
                Number = _undoList[_undoList.Count - 1];

                _redoList.Add(_undoList[_undoList.Count - 1]);
                _undoList.RemoveAt(_undoList.Count - 1);
            }
        }

        /// <summary>
        /// Redo to the value previously undone. The list is clear when key is handled
        /// </summary>
        public new void Redo()
        {
            if (_redoList.Count > 0)
            {
                AddUndoInList(Number, false);
                Number = _redoList[_redoList.Count - 1];
                _redoList.RemoveAt(_redoList.Count - 1);
            }
        }

        /// <summary>
        /// Get or set for indicate if control CanUndo
        /// </summary>
        public new bool IsUndoEnabled { get; set; } = true;

        /// <summary>
        /// Clear the undo list
        /// </summary>
        public void ClearUndoList() => _undoList.Clear();

        /// <summary>
        /// Check if the control can undone to a previous value
        /// </summary>
        /// <returns></returns>
        public new bool CanUndo() => IsUndoEnabled && _undoList.Count > 0;

        #endregion Undo/Redo

        #region Public Methods

        /// <summary>
        /// Reset the number to zero.
        /// </summary>
        public new void Clear() => Number = 0M;

        /// <summary>
        /// Set number to positive
        /// </summary>
        public void SetPositive() { if (Number < 0) Number *= -1; }

        /// <summary>
        /// Set number to negative
        /// </summary>
        public void SetNegative() { if (Number > 0) Number *= -1; }

        /// <summary>
        /// Alternate value to Negative-Positive and Positive-Negative
        /// </summary>
        public void InvertValue() => Number *= -1;

        #endregion Other function

        #region Clipboard
        /// <summary>
        /// Paste if is a number on clipboard
        /// </summary>
        private void PasteFromClipBoard()
        {
            try
            {
                switch (GetBindingExpression(TextProperty).ParentBinding.StringFormat)
                {
                    case "P0":
                    case "P":
                    case "P1":
                    case "P2":
                    case "P3":
                    case "P4":
                    case "P5":
                    case "P6":
                        Number = decimal.Parse(Clipboard.GetText());
                        break;
                    default:
                        Number = Math.Round(decimal.Parse(Clipboard.GetText()), GetDigitCount());
                        break;
                }

            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Copy the property Number to Control
        /// </summary>
        private void CopyToClipBoard()
        {
            Clipboard.Clear();
            Clipboard.SetText(Number.ToString(Culture));
        }
        #endregion Clipboard

        #region Add/remove value Popup 
        /// <summary>
        /// Show popup for add/remove value
        /// </summary>
        private void ShowAddPopup()
        {
            if (CanShowAddPanel)
            {
                //Initialize somes Child object
                var grid = new Grid { Background = Brushes.White };

                var ctbPopup = new CurrencyTextBox
                {
                    CanShowAddPanel = false,
                    IsCalculPanelMode = true,
                    StringFormat = StringFormat,
                    Background = Brushes.WhiteSmoke
                };

                _popup = new Popup
                {
                    Width = ActualWidth,
                    Height = 32,
                    PopupAnimation = PopupAnimation.Fade,
                    Placement = PlacementMode.Bottom,
                    PlacementTarget = this,
                    StaysOpen = false,
                    Child = grid,
                    IsOpen = true
                };

                //Set object properties                                         
                ctbPopup.NumberChanged += Ctb_NumberChanged;
                ctbPopup.PopupClosed += CtbPopup_PopupClosed;

                _numberBeforePopup = Number;
                _popupLabel = new Label { Content = "+" };

                //ColumnDefinition
                var c1 = new ColumnDefinition { Width = new GridLength(20, GridUnitType.Auto) };
                var c2 = new ColumnDefinition { Width = new GridLength(80, GridUnitType.Star) };
                grid.ColumnDefinitions.Add(c1);
                grid.ColumnDefinitions.Add(c2);
                Grid.SetColumn(_popupLabel, 0);
                Grid.SetColumn(ctbPopup, 1);

                //Add object 
                grid.Children.Add(_popupLabel);
                grid.Children.Add(ctbPopup);

                ctbPopup.Focus();
            }
        }

        private void CtbPopup_PopupClosed(object sender, EventArgs e) => Focus();
        #endregion Add/remove value Popup


        private class NumberParts
        {
            public bool IsNegative { get; set; }
            public int DigitCount { get; set; }
            public long IntegerPart { get; set; }
            public long DecimalPart { get; set; }
            public string DecimalSeparator { get; set; }
        }

        public enum InputTypeEnum
        {
            Simplified,
            Extended,
            Segmented
        }

        public enum ExtendedInputCurrentModeEnum
        {
            Integer,
            Decimal
        }
    }
}
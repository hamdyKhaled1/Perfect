// ========================================
// حسابات الحاج إمام - JavaScript
// ========================================

$(document).ready(function () {

    // Auto-dismiss success alerts after 5 seconds
    setTimeout(function () {
        $('#successAlert').fadeOut('slow');
    }, 5000);

    // Wire up calculation events for transaction forms
    $('.calc-input').on('input change', function () {
        recalculateAll();
    });

    $('#AdditionAmount, #AdditionNotes').on('input change', updateAdditionNotesValidation);
    $('#DiscountAmount, #DiscountNotes').on('input change', updateDiscountNotesValidation);
    $('[data-transaction-next]').on('click', function () {
        if (updateAdditionNotesValidation() && updateDiscountNotesValidation()) {
            showTransactionStep('meal');
        } else if (!updateAdditionNotesValidation()) {
            $('#AdditionNotes').trigger('focus');
        } else {
            $('#DiscountNotes').trigger('focus');
        }
    });
    $('[data-transaction-previous]').on('click', function () {
        showTransactionStep('accounts');
    });
    $('[data-transaction-form]').each(function () {
        showTransactionStep($(this).data('active-step') === 'meal' ? 'meal' : 'accounts');
    });
    $('[data-transaction-form]').on('submit', function (event) {
        if ($(event.originalEvent?.submitter).is('[data-transaction-collection-submit]')) {
            return;
        }

        var additionIsValid = updateAdditionNotesValidation();
        var discountIsValid = updateDiscountNotesValidation();
        if (!additionIsValid || !discountIsValid) {
            event.preventDefault();
            showTransactionStep('accounts');
            if (!additionIsValid) {
                $('#AdditionNotes').trigger('focus');
            } else {
                $('#DiscountNotes').trigger('focus');
            }
        }
    });

    // Initial calculation on page load (for edit forms)
    if ($('.calc-input').length > 0) {
        recalculateAll();
    }
    updateAdditionNotesValidation();
    updateDiscountNotesValidation();

    $('[data-bulk-meal-input]').on('input change', function () {
        updateBulkMealRow($(this).closest('[data-bulk-meal-row]'));
        updateBulkMealTotals();
    });
    $('[data-bulk-meal-search]').on('input', function () {
        var searchTerm = String($(this).val() || '').trim().toLocaleLowerCase();
        $('[data-bulk-meal-row]').each(function () {
            var clientName = $(this).find('[data-bulk-meal-client-name]').text().toLocaleLowerCase();
            $(this).toggle(clientName.indexOf(searchTerm) !== -1);
        });
    });
    $('[data-bulk-meal-farm-price]').on('input change', function () {
        $('[data-bulk-meal-row]').each(function () {
            updateBulkMealRow($(this));
        });
        updateBulkMealTotals();
    });
    var farmBalanceTimer;
    var farmBalanceRequest = 0;
    $('[data-bulk-meal-farm-name]').on('input', function () {
        clearTimeout(farmBalanceTimer);
        var farmNameInput = $(this);
        var farmName = String($(this).val() || '').trim();
        var requestId = ++farmBalanceRequest;
        var previousAccount = $('[data-bulk-previous-farm-account]').removeData('balance');
        var totalAccount = $('[data-bulk-total-farm-account]');

        if (!farmName) {
            previousAccount.text('اكتب اسم المزرعة');
            totalAccount.text('—');
            updateBulkMealTotals();
            return;
        }

        previousAccount.text('جارٍ تحميل الحساب...');
        totalAccount.text('جارٍ تحميل الحساب...');
        farmBalanceTimer = setTimeout(function () {
            $.getJSON(farmNameInput.data('farm-balance-url'), { farmName: farmName })
                .done(function (result) {
                    if (requestId !== farmBalanceRequest) return;
                    previousAccount.data('balance', Number(result.balance) || 0)
                        .text(formatNumber(Number(result.balance) || 0));
                    updateBulkMealTotals();
                })
                .fail(function () {
                    if (requestId !== farmBalanceRequest) return;
                    previousAccount.text('تعذر تحميل الحساب السابق');
                    $('[data-bulk-total-farm-account]').text('—');
                });
        }, 300);
    });
    $('[data-bulk-meal-row]').each(function () {
        updateBulkMealRow($(this));
    });
    updateBulkMealTotals();
    if ($('[data-bulk-meal-farm-name]').val()) {
        $('[data-bulk-meal-farm-name]').trigger('input');
    }

    // Delete confirmation
    $('form[data-confirm]').on('submit', function (e) {
        var message = $(this).data('confirm') || 'هل أنت متأكد من الحذف؟';
        if (!confirm(message)) {
            e.preventDefault();
        }
    });

    // Search across all clients, not just the currently displayed page.
    var clientSearchTimer;
    $('#clientSearch').on('input', function () {
        clearTimeout(clientSearchTimer);
        clientSearchTimer = setTimeout(function () {
            $('#clientFilterForm').trigger('submit');
        }, 300);
    });
});

function showTransactionStep(step) {
    var showMeal = step === 'meal';
    $('[data-transaction-step="accounts"]').toggle(!showMeal);
    $('[data-transaction-step="meal"]').toggle(showMeal);
    $('[data-transaction-next]').toggle(!showMeal);
    $('[data-transaction-collection-submit]').toggle(!showMeal);
    $('[data-transaction-previous], [data-transaction-submit]').toggle(showMeal);
}

// ========================================
// Calculation Functions
// ========================================

function getVal(id) {
    var val = parseFloat($('#' + id).val());
    return isNaN(val) ? 0 : val;
}

function setDisplay(id, value) {
    var formatted = formatNumber(value);
    $('#' + id).text(formatted);
    // Also update hidden input if exists
    $('#' + id + '_input').val(value.toFixed(2));
}

function recalculateAll() {
    // Section 1: Financials
    var oldBalance = getVal('OldBalance');
    var cashCollection = getVal('CashCollection');
    var electronicCollection = getVal('ElectronicCollection');
    var additionAmount = getVal('AdditionAmount');
    var discountAmount = getVal('DiscountAmount');

    var remainingAfterCollection = oldBalance - (cashCollection + electronicCollection);
    setDisplay('remainingAfterCollection', remainingAfterCollection);

    var subTotal = remainingAfterCollection + additionAmount - discountAmount;
    setDisplay('subTotal', subTotal);
    $('[data-account-total]').text(formatNumber(subTotal));

    // Section 2: Meal/Delivery
    var grossWeight = parseNumberExpression($('[data-gross-weight-expression]').val());
    var emptyWeight = parseNumberExpression($('[data-empty-weight-expression]').val());

    if (!Number.isFinite(grossWeight) || !Number.isFinite(emptyWeight)) {
        $('#netWeight, #mealTotal, #totalDue').text('-');
        return;
    }
    var netWeight = grossWeight - emptyWeight;
    setDisplay('netWeight', netWeight);

    var extraField = getVal('ExtraField');
    var pricePerKilo = getVal('FarmPrice') + extraField;
    $('#PricePerKilo').val(pricePerKilo.toFixed(2));

    var mealTotal = netWeight * pricePerKilo;
    setDisplay('mealTotal', mealTotal);

    var totalDue = mealTotal + subTotal;
    setDisplay('totalDue', totalDue);
}

function updateDiscountNotesValidation() {
    var hasDiscount = getVal('DiscountAmount') > 0;
    var notes = ($('#DiscountNotes').val() || '').trim();
    var error = $('#DiscountNotesValidation');
    var isValid = !hasDiscount || notes.length > 0;

    error.toggle(!isValid);
    return isValid;
}

function updateAdditionNotesValidation() {
    var hasAddition = getVal('AdditionAmount') !== 0;
    var notes = ($('#AdditionNotes').val() || '').trim();
    var error = $('#AdditionNotesValidation');
    var isValid = !hasAddition || notes.length > 0;

    error.toggle(!isValid);
    return isValid;
}

function updateBulkMealRow(row) {
    if (!row.length) return;

    var grossWeight = parseNumberExpression(row.find('[data-gross-expression]').val());
    var emptyWeight = parseNumberExpression(row.find('[data-empty-expression]').val());
    var farmPrice = parseFloat($('[data-bulk-meal-farm-price]').val()) || 0;
    var priceIncrement = parseFloat(row.find('[data-price-increment]').val()) || 0;

    if (!Number.isFinite(grossWeight) || !Number.isFinite(emptyWeight)) {
        row.find('[data-net-weight], [data-meal-total]').text('-');
        return;
    }

    var netWeight = grossWeight - emptyWeight;
    var pricePerKilo = farmPrice + priceIncrement;
    var mealTotal = netWeight * pricePerKilo;

    row.find('[data-net-weight]').text(formatNumber(netWeight));
    row.find('[data-price-per-kilo]').text(formatNumber(pricePerKilo));
    row.find('[data-meal-total]').text(formatNumber(mealTotal));
}

function updateBulkMealTotals() {
    var grossTotal = 0;
    var emptyTotal = 0;
    var netTotal = 0;
    var mealTotal = 0;
    var hasInvalidWeight = false;
    var farmPrice = parseFloat($('[data-bulk-meal-farm-price]').val()) || 0;
    var farmWeight = parseFloat($('[data-bulk-meal-farm-weight]').val()) || 0;

    $('[data-bulk-meal-row]').each(function () {
        var row = $(this);
        var grossWeight = parseNumberExpression(row.find('[data-gross-expression]').val());
        var emptyWeight = parseNumberExpression(row.find('[data-empty-expression]').val());

        if (!Number.isFinite(grossWeight) || !Number.isFinite(emptyWeight)) {
            hasInvalidWeight = true;
            return;
        }

        var netWeight = grossWeight - emptyWeight;
        var pricePerKilo = (parseFloat($('[data-bulk-meal-farm-price]').val()) || 0) +
            (parseFloat(row.find('[data-price-increment]').val()) || 0);

        grossTotal += grossWeight;
        emptyTotal += emptyWeight;
        netTotal += netWeight;
        mealTotal += netWeight * pricePerKilo;
    });

    $('[data-bulk-total-gross]').text(hasInvalidWeight ? '-' : formatNumber(grossTotal));
    $('[data-bulk-total-empty]').text(hasInvalidWeight ? '-' : formatNumber(emptyTotal));
    $('[data-bulk-total-net]').text(hasInvalidWeight ? '-' : formatNumber(netTotal));
    $('[data-bulk-total-meal]').text(hasInvalidWeight ? '-' : formatNumber(mealTotal));
    var farmAccount = farmWeight * farmPrice;
    $('[data-bulk-farm-account]').text(formatNumber(farmAccount));
    var previousFarmAccount = $('[data-bulk-previous-farm-account]').data('balance');
    if (Number.isFinite(previousFarmAccount)) {
        $('[data-bulk-total-farm-account]').text(formatNumber(previousFarmAccount + farmAccount));
    } else if (!$('[data-bulk-meal-farm-name]').val()) {
        $('[data-bulk-total-farm-account]').text('—');
    }
    var weightDifference = farmWeight - netTotal;
    $('[data-bulk-weight-difference]').text(hasInvalidWeight ? '-' : formatNumber(weightDifference));
    $('[data-bulk-farm-profit]').text(hasInvalidWeight ? '-' : formatNumber(
        weightDifference * farmPrice + mealTotal - farmWeight * farmPrice));
    $('[data-bulk-farm-final-total]').text(hasInvalidWeight ? '-' : formatNumber(mealTotal - farmWeight * farmPrice));
}

function parseNumberExpression(expression) {
    var normalized = String(expression || '').replace(/[٠-٩]/g, function (digit) {
        return String(digit.charCodeAt(0) - 1632);
    }).replace(/[۰-۹]/g, function (digit) {
        return String(digit.charCodeAt(0) - 1776);
    }).replace(/[٫]/g, '.').replace(/[٬,\s]/g, '');

    if (!normalized) return 0;

    var tokens = normalized.match(/(?:\d+(?:\.\d*)?|\.\d+)|[()+*/-]/g);
    if (!tokens || tokens.join('') !== normalized) return NaN;

    var position = 0;

    function parseExpression() {
        var value = parseTerm();
        while (tokens[position] === '+' || tokens[position] === '-') {
            var operator = tokens[position++];
            var next = parseTerm();
            value = operator === '+' ? value + next : value - next;
        }
        return value;
    }

    function parseTerm() {
        var value = parseFactor();
        while (tokens[position] === '*' || tokens[position] === '/') {
            var operator = tokens[position++];
            var next = parseFactor();
            value = operator === '*' ? value * next : next === 0 ? NaN : value / next;
        }
        return value;
    }

    function parseFactor() {
        if (tokens[position] === '+' || tokens[position] === '-') {
            var sign = tokens[position++] === '-' ? -1 : 1;
            return sign * parseFactor();
        }
        if (tokens[position] === '(') {
            position++;
            var value = parseExpression();
            if (tokens[position++] !== ')') return NaN;
            return value;
        }
        var token = tokens[position++];
        return token && /^\d|^\./.test(token) ? parseFloat(token) : NaN;
    }

    var result = parseExpression();
    return position === tokens.length && Number.isFinite(result) ? result : NaN;
}

// ========================================
// Number Formatting
// ========================================

function formatNumber(num) {
    if (isNaN(num)) return '0';
    return num.toLocaleString('ar-EG', {
        minimumFractionDigits: 0,
        maximumFractionDigits: 4
    });
}

// ========================================
// Print Function
// ========================================

function printPage() {
    window.print();
}

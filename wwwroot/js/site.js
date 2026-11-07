// ========================================
// حسابات الحاج إمام - JavaScript
// ========================================

$(document).ready(function () {

    function toEnglishDigits(value) {
        return value.replace(/[٠-٩۰-۹]/g, function (digit) {
            var code = digit.charCodeAt(0);
            return String(code >= 0x06f0 ? code - 0x06f0 : code - 0x0660);
        });
    }

    function normalizeTextNode(textNode) {
        var parent = textNode.parentElement;
        if (!parent || parent.closest('script, style, textarea, bdi[dir="ltr"]')) return;

        var normalized = toEnglishDigits(textNode.nodeValue)
            .replace(/(\d[\d,٬.]*)-(?=\s|$)/g, '-$1');
        if (normalized !== textNode.nodeValue)
            textNode.nodeValue = normalized;

        var numberPattern = /-?\d[\d,٬.\/]*/g;
        if (!numberPattern.test(normalized)) return;

        numberPattern.lastIndex = 0;
        var fragment = document.createDocumentFragment();
        var lastIndex = 0;
        var match;
        while ((match = numberPattern.exec(normalized)) !== null) {
            if (match.index > lastIndex)
                fragment.appendChild(document.createTextNode(normalized.slice(lastIndex, match.index)));

            var number = document.createElement('bdi');
            number.dir = 'ltr';
            number.textContent = match[0];
            fragment.appendChild(number);
            lastIndex = match.index + match[0].length;
        }

        if (lastIndex < normalized.length)
            fragment.appendChild(document.createTextNode(normalized.slice(lastIndex)));

        textNode.parentNode.replaceChild(fragment, textNode);
    }

    function normalizeDisplayedDigits(root) {
        if (root.nodeType === Node.ELEMENT_NODE && root.matches('script, style, textarea, bdi[dir="ltr"]'))
            return;

        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        var textNode;
        var textNodes = [];
        while ((textNode = walker.nextNode()))
            textNodes.push(textNode);
        textNodes.forEach(normalizeTextNode);
    }

    normalizeDisplayedDigits(document.body);
    var digitObserver = new MutationObserver(function (mutations) {
        mutations.forEach(function (mutation) {
            if (mutation.type === 'characterData') {
                normalizeDisplayedDigits(mutation.target.parentElement || document.body);
            } else {
                mutation.addedNodes.forEach(function (node) {
                    if (node.nodeType === Node.TEXT_NODE) {
                        normalizeTextNode(node);
                    } else if (node.nodeType === Node.ELEMENT_NODE) {
                        normalizeDisplayedDigits(node);
                    }
                });
            }
        });
    });
    digitObserver.observe(document.body, { childList: true, characterData: true, subtree: true });

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

    // Treasury Selection Wiring
    function updateTreasuryOptions(selectedOwner, preserveValue) {
        var $elecSelect = $('#ElectronicTreasuryAccount');
        var $cashSelect = $('#CashTreasuryAccount');
        if (!$elecSelect.length || !$cashSelect.length) return;

        var cashAccountByOwner = {
            AbuMazen: 'AbuMazenCash',
            HajjImam: 'HajjImamCash',
            MohamedAbdelrahman: 'MohamedAbdelrahmanCash'
        };
        if (cashAccountByOwner[selectedOwner])
            $cashSelect.val(cashAccountByOwner[selectedOwner]);

        var currentElecVal = preserveValue ? $elecSelect.val() : null;

        // Filter electronic options
        $elecSelect.find('option').each(function () {
            var owner = $(this).data('owner');
            if (!owner || owner === selectedOwner) {
                $(this).show().prop('disabled', false);
            } else {
                $(this).hide().prop('disabled', true);
            }
        });

        // Ensure a valid option is selected
        var $validOptions = $elecSelect.find('option:enabled');
        if (currentElecVal && $validOptions.filter('[value="' + currentElecVal + '"]').length) {
            $elecSelect.val(currentElecVal);
        } else if ($validOptions.length) {
            $elecSelect.val($validOptions.first().val());
        }
    }

    $('input[name="treasuryOwnerRadio"]').on('change', function () {
        updateTreasuryOptions($(this).val(), false);
    });

    $('#CashTreasuryAccount').on('change', function () {
        var owner = $(this).find('option:selected').data('owner');
        $('input[name="treasuryOwnerRadio"][value="' + owner + '"]').prop('checked', true);
        updateTreasuryOptions(owner, true);
    });

    if ($('input[name="treasuryOwnerRadio"]').length) {
        var initialOwner = $('input[name="treasuryOwnerRadio"]:checked').val() || 'AbuMazen';
        updateTreasuryOptions(initialOwner, true);
    }

    var bulkMealForm = $('[data-bulk-meal-form]');
    if (bulkMealForm.length) {
        var bulkMealDraftKey = 'bulk-meal-draft-v4:' + window.location.pathname;
        var bulkMealDraftStatus = bulkMealForm.find('[data-bulk-meal-draft-status]');
        var bulkMealPasswordConfirmed = false;
        var bulkMealPasswordModalElement = document.getElementById('bulkMealPasswordModal');
        var bulkMealPasswordModal = bootstrap.Modal.getOrCreateInstance(bulkMealPasswordModalElement);

        function showBulkMealDraftError(error) {
            console.error('Unable to persist the bulk meal draft in this tab.', error);
            bulkMealDraftStatus
                .text('تعذر حفظ مسودة الوجبات في هذا التبويب. ستظل البيانات موجودة ما دمت على الصفحة، لكن قد تفقدها إذا انتقلت لصفحة أخرى.')
                .removeClass('d-none');
        }

        function getBulkMealDraft() {
            var draft = { fields: {}, rows: {} };
            bulkMealForm.find(':input[name]').each(function () {
                var input = $(this);
                if (input.attr('type') === 'hidden') return;

                var match = input.attr('name').match(/^Rows\[(\d+)\]\.(.+)$/);
                if (!match) {
                    draft.fields[input.attr('name')] = input.val();
                    return;
                }

                var clientId = input.closest('[data-bulk-meal-row]')
                    .find('input[name$=".ClientId"]').val();
                if (!clientId) return;

                draft.rows[clientId] = draft.rows[clientId] || {};
                draft.rows[clientId][match[2]] = input.val();
            });
            return draft;
        }

        function applyBulkMealDraft(draft) {
            bulkMealForm.find(':input[name]').each(function () {
                var input = $(this);
                if (input.attr('type') === 'hidden') return;

                var match = input.attr('name').match(/^Rows\[(\d+)\]\.(.+)$/);
                if (!match) {
                    if (Object.prototype.hasOwnProperty.call(draft.fields || {}, input.attr('name'))) {
                        input.val(draft.fields[input.attr('name')]);
                    }
                    return;
                }

                var clientId = input.closest('[data-bulk-meal-row]')
                    .find('input[name$=".ClientId"]').val();
                var rowDraft = (draft.rows || {})[clientId];
                if (rowDraft && Object.prototype.hasOwnProperty.call(rowDraft, match[2])) {
                    input.val(rowDraft[match[2]]);
                }
            });
        }

        var bulkMealWasSaved = bulkMealForm.attr('data-bulk-meal-saved') === 'true';
        try {
            if (bulkMealWasSaved) {
                sessionStorage.removeItem(bulkMealDraftKey);
            } else {
                var savedBulkMealDraft = sessionStorage.getItem(bulkMealDraftKey);
                if (savedBulkMealDraft) {
                    applyBulkMealDraft(JSON.parse(savedBulkMealDraft));
                    $('[data-bulk-meal-input]').trigger('change');
                }
            }
        } catch (error) {
            showBulkMealDraftError(error);
        }

        bulkMealForm.on('input change', ':input[name]', function () {
            try {
                sessionStorage.setItem(bulkMealDraftKey, JSON.stringify(getBulkMealDraft()));
            } catch (error) {
                showBulkMealDraftError(error);
            }
        });

        bulkMealForm.on('submit', function (event) {
            if (!bulkMealPasswordConfirmed) {
                event.preventDefault();
                bulkMealPasswordModal.show();
                window.setTimeout(function () {
                    $('[data-bulk-meal-save-password-input]').trigger('focus');
                }, 150);
                return;
            }

            try {
                sessionStorage.setItem(bulkMealDraftKey, JSON.stringify(getBulkMealDraft()));
            } catch (error) {
                showBulkMealDraftError(error);
            }
        });
        $('[data-bulk-meal-password-confirm]').on('click', function () {
            var password = $('[data-bulk-meal-save-password-input]').val();
            if (!password) {
                $('[data-bulk-meal-password-error]').removeClass('d-none');
                return;
            }

            $('[data-bulk-meal-password-error]').addClass('d-none');
            bulkMealForm.find('[data-bulk-meal-save-password]').val(password);
            bulkMealPasswordConfirmed = true;
            bulkMealPasswordModal.hide();
            bulkMealForm[0].requestSubmit();
        });
        $('[data-bulk-meal-save-password-input]').on('input', function () {
            $('[data-bulk-meal-password-error]').addClass('d-none');
        }).on('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                $('[data-bulk-meal-password-confirm]').trigger('click');
            }
        });
    }

    // قائمة العملاء: بحث فوري وتنقل بين الصفحات من غير إعادة تحميل (زي صفحة إدخال الوجبات).
    var clientRows = $('[data-client-row]');
    if (clientRows.length) {
        var clientPage = 1;

        function addClientPageLink(label, pageNumber, disabled, active) {
            var item = $('<li>').addClass('page-item')
                .toggleClass('disabled', disabled)
                .toggleClass('active', active);
            var link = $('<button type="button">').addClass('page-link').text(label)
                .attr('aria-label', label)
                .prop('disabled', disabled)
                .on('click', function () {
                    clientPage = pageNumber;
                    updateClientPagination();
                });
            item.append(link);
            return item;
        }

        function updateClientPagination() {
            var searchTerm = String($('[data-client-search]').val() || '').trim().toLocaleLowerCase();
            var pageSize = parseInt($('[data-client-page-size]').val(), 10) || 10;
            var matchingRows = clientRows.filter(function () {
                var searchText = String($(this).attr('data-client-search-text') || '').toLocaleLowerCase();
                return searchText.indexOf(searchTerm) !== -1;
            });

            var totalPages = Math.ceil(matchingRows.length / pageSize);
            clientPage = Math.min(Math.max(clientPage, 1), Math.max(totalPages, 1));
            var firstRow = (clientPage - 1) * pageSize;

            clientRows.hide();
            var visibleRows = matchingRows.slice(firstRow, firstRow + pageSize);
            visibleRows.show();
            visibleRows.each(function (index) {
                $(this).find('[data-client-number]').text(firstRow + index + 1);
            });

            $('[data-client-table-card]').toggleClass('d-none', matchingRows.length === 0);
            $('[data-client-no-results]').toggleClass('d-none', matchingRows.length > 0);
            $('[data-client-pagination]').toggleClass('d-none', matchingRows.length === 0);

            var pageLinks = $('[data-client-page-links]').empty();
            if (!matchingRows.length) {
                $('[data-client-page-summary]').text('لا توجد نتائج');
                return;
            }

            $('[data-client-page-summary]').text(
                'عرض ' + (firstRow + 1) + ' إلى ' + Math.min(firstRow + pageSize, matchingRows.length) +
                ' من ' + matchingRows.length + ' عميل');

            if (totalPages > 1) {
                var firstPage = Math.max(1, clientPage - 2);
                var lastPage = Math.min(totalPages, firstPage + 4);
                firstPage = Math.max(1, lastPage - 4);

                pageLinks.append(addClientPageLink('الأولى', 1, clientPage === 1, false));
                pageLinks.append(addClientPageLink('السابق', clientPage - 1, clientPage === 1, false));
                for (var pageNumber = firstPage; pageNumber <= lastPage; pageNumber++) {
                    pageLinks.append(addClientPageLink(String(pageNumber), pageNumber, false, pageNumber === clientPage));
                }
                pageLinks.append(addClientPageLink('التالي', clientPage + 1, clientPage === totalPages, false));
                pageLinks.append(addClientPageLink('الأخيرة', totalPages, clientPage === totalPages, false));
            }
        }

        $('[data-client-search]').on('input', function () {
            clientPage = 1;
            updateClientPagination();
        }).on('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
            }
        });
        $('[data-client-page-size]').on('change', function () {
            clientPage = 1;
            updateClientPagination();
        });

        updateClientPagination();
    }

    // عملاء الخارج: بحث فوري وتنقل بين الصفحات من غير إعادة تحميل.
    var outsideRows = $('[data-outside-row]');
    if (outsideRows.length) {
        var outsidePage = 1;

        function addOutsidePageLink(label, pageNumber, disabled, active) {
            var item = $('<li>').addClass('page-item')
                .toggleClass('disabled', disabled)
                .toggleClass('active', active);
            var link = $('<button type="button">').addClass('page-link').text(label)
                .attr('aria-label', label)
                .prop('disabled', disabled)
                .on('click', function () {
                    outsidePage = pageNumber;
                    updateOutsidePagination();
                });
            item.append(link);
            return item;
        }

        function updateOutsidePagination() {
            var searchTerm = String($('[data-outside-search]').val() || '').trim().toLocaleLowerCase();
            var pageSize = parseInt($('[data-outside-page-size]').val(), 10) || 10;
            var matchingRows = outsideRows.filter(function () {
                var searchText = String($(this).attr('data-outside-search-text') || '').toLocaleLowerCase();
                return searchText.indexOf(searchTerm) !== -1;
            });

            var totalPages = Math.ceil(matchingRows.length / pageSize);
            outsidePage = Math.min(Math.max(outsidePage, 1), Math.max(totalPages, 1));
            var firstRow = (outsidePage - 1) * pageSize;

            outsideRows.hide();
            var visibleRows = matchingRows.slice(firstRow, firstRow + pageSize);
            visibleRows.show();
            visibleRows.each(function (index) {
                $(this).find('[data-outside-number]').text(firstRow + index + 1);
            });

            $('[data-outside-table-card]').toggleClass('d-none', matchingRows.length === 0);
            $('[data-outside-no-results]').toggleClass('d-none', matchingRows.length > 0);
            $('[data-outside-pagination]').toggleClass('d-none', matchingRows.length === 0);

            var pageLinks = $('[data-outside-page-links]').empty();
            if (!matchingRows.length) {
                $('[data-outside-page-summary]').text('لا توجد نتائج');
                return;
            }

            $('[data-outside-page-summary]').text(
                'عرض ' + (firstRow + 1) + ' إلى ' + Math.min(firstRow + pageSize, matchingRows.length) +
                ' من ' + matchingRows.length + ' عميل');

            if (totalPages > 1) {
                var firstPage = Math.max(1, outsidePage - 2);
                var lastPage = Math.min(totalPages, firstPage + 4);
                firstPage = Math.max(1, lastPage - 4);

                pageLinks.append(addOutsidePageLink('الأولى', 1, outsidePage === 1, false));
                pageLinks.append(addOutsidePageLink('السابق', outsidePage - 1, outsidePage === 1, false));
                for (var pageNumber = firstPage; pageNumber <= lastPage; pageNumber++) {
                    pageLinks.append(addOutsidePageLink(String(pageNumber), pageNumber, false, pageNumber === outsidePage));
                }
                pageLinks.append(addOutsidePageLink('التالي', outsidePage + 1, outsidePage === totalPages, false));
                pageLinks.append(addOutsidePageLink('الأخيرة', totalPages, outsidePage === totalPages, false));
            }
        }

        $('[data-outside-search]').on('input', function () {
            outsidePage = 1;
            updateOutsidePagination();
        }).on('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
            }
        });
        $('[data-outside-page-size]').on('change', function () {
            outsidePage = 1;
            updateOutsidePagination();
        });

        updateOutsidePagination();
    }

    $(document).on('input change focus', '[data-bulk-meal-input]', function () {
        autoSizeBulkMealInput(this);
        var $row = $(this).closest('[data-bulk-meal-row]');
        updateBulkMealRow($row);
        updateBulkMealTotals();
        updateBulkMealRowHint($row);
    });

    // تكبير خانة الإدخال مع الرقم المكتوب حتى يظهر الرقم كله من غير ما يتقطع.
    var bulkMealMeasureCanvas = document.createElement('canvas');
    var bulkMealMeasureContext = bulkMealMeasureCanvas.getContext('2d');

    function autoSizeBulkMealInput(element) {
        if (!element || !element.matches('[data-bulk-meal-input]') || element.matches('[data-notes]')) {
            return;
        }

        var style = window.getComputedStyle(element);
        var text = String(element.value || '') || element.getAttribute('placeholder') || '0';
        bulkMealMeasureContext.font = style.fontWeight + ' ' + style.fontSize + ' ' + style.fontFamily;
        var contentWidth = bulkMealMeasureContext.measureText(text).width;
        var chromeWidth = parseFloat(style.paddingLeft) + parseFloat(style.paddingRight) +
            parseFloat(style.borderLeftWidth) + parseFloat(style.borderRightWidth) + 6;
        if (element.type === 'number') {
            chromeWidth += 18;
        }

        var rootFontSize = parseFloat(window.getComputedStyle(document.documentElement).fontSize) || 16;
        var minWidth = rootFontSize * 5;
        var maxWidth = rootFontSize * 16;
        var width = Math.min(Math.max(contentWidth + chromeWidth, minWidth), maxWidth);
        element.style.width = Math.round(width) + 'px';
        element.style.minWidth = Math.round(width) + 'px';
    }

    $('[data-bulk-meal-input]').each(function () {
        autoSizeBulkMealInput(this);
    });

    function updateBulkMealRowHint($row) {
        if (!$row || !$row.length) return;
        var $hintTop = $('[data-bulk-row-hint-top]');
        if (!$hintTop.length) return;

        var hasPanehMeal = $row.find('[data-paneh-gross-expression]').length > 0;
        var grossVal = String($row.find('[data-gross-expression]').val() || '').trim();
        var emptyVal = String($row.find('[data-empty-expression]').val() || '').trim();
        var notes = String($row.find('[data-notes]').val() || '').trim();

        var panehGrossVal = hasPanehMeal ? String($row.find('[data-paneh-gross-expression]').val() || '').trim() : '';
        var panehEmptyVal = hasPanehMeal ? String($row.find('[data-paneh-empty-expression]').val() || '').trim() : '';

        // الزيادة على سعر المزرعة قيمة محفوظة لكل عميل، فلا تُعتبر إدخالاً جديداً.
        var hasAny = grossVal || emptyVal || notes || panehGrossVal || panehEmptyVal;

        if (!hasAny) {
            $hintTop.addClass('d-none');
            return;
        }

        var isComplete = false;
        if (hasPanehMeal) {
            isComplete = !!(grossVal && emptyVal && panehGrossVal && panehEmptyVal);
        } else {
            isComplete = !!(grossVal && emptyVal);
        }

        if (!isComplete) {
            var clientName = $row.find('[data-bulk-meal-client-name]').first().text().trim().split('\n')[0].trim();
            $hintTop.text('أكمل باقي بيانات العميل: ' + clientName);
            $hintTop.removeClass('d-none');
        } else {
            $hintTop.addClass('d-none');
        }
    }
    $('[data-expression-operator]').on('click', function () {
        var input = document.getElementById($(this).data('expression-target'));
        if (!input) return;

        var start = input.selectionStart ?? input.value.length;
        var end = input.selectionEnd ?? start;
        input.setRangeText(String($(this).data('expression-operator')), start, end, 'end');
        input.focus();
        input.dispatchEvent(new Event('input', { bubbles: true }));
    });
    var bulkMealRows = $('[data-bulk-meal-row]');
    var bulkMealPage = 1;
    function updateBulkMealPagination() {
        var searchTerm = String($('[data-bulk-meal-search]').val() || '').trim().toLocaleLowerCase();
        var pageSize = parseInt($('[data-bulk-meal-page-size]').val(), 10) || 10;
        var matchingRows = bulkMealRows.filter(function () {
            var clientName = $(this).find('[data-bulk-meal-client-name]').text().toLocaleLowerCase();
            return clientName.indexOf(searchTerm) !== -1;
        });
        var totalPages = Math.ceil(matchingRows.length / pageSize);
        bulkMealPage = Math.min(Math.max(bulkMealPage, 1), Math.max(totalPages, 1));
        var firstRow = (bulkMealPage - 1) * pageSize;

        bulkMealRows.hide();
        matchingRows.slice(firstRow, firstRow + pageSize).show();

        var pagination = $('[data-bulk-meal-pagination]');
        var pageLinks = $('[data-bulk-meal-page-links]').empty();
        pagination.toggle(matchingRows.length > 0);
        if (!matchingRows.length) return;

        $('[data-bulk-meal-page-summary]').text(
            'عرض ' + (firstRow + 1) + ' إلى ' + Math.min(firstRow + pageSize, matchingRows.length) +
            ' من ' + matchingRows.length + ' عميل');

        var firstPage = Math.max(1, bulkMealPage - 2);
        var lastPage = Math.min(totalPages, firstPage + 4);
        firstPage = Math.max(1, lastPage - 4);

        function addPageLink(label, pageNumber, disabled, active) {
            var item = $('<li>').addClass('page-item')
                .toggleClass('disabled', disabled)
                .toggleClass('active', active);
            var link = $('<button type="button">').addClass('page-link').text(label)
                .attr('aria-label', label)
                .prop('disabled', disabled)
                .on('click', function () {
                    bulkMealPage = pageNumber;
                    updateBulkMealPagination();
                });
            item.append(link);
            pageLinks.append(item);
        }

        if (totalPages > 1) {
            addPageLink('الأولى', 1, bulkMealPage === 1, false);
            addPageLink('السابق', bulkMealPage - 1, bulkMealPage === 1, false);
            for (var pageNumber = firstPage; pageNumber <= lastPage; pageNumber++) {
                addPageLink(String(pageNumber), pageNumber, false, pageNumber === bulkMealPage);
            }
            addPageLink('التالي', bulkMealPage + 1, bulkMealPage === totalPages, false);
            addPageLink('الأخيرة', totalPages, bulkMealPage === totalPages, false);
        }
    }
    $('[data-bulk-meal-search]').on('input', function () {
        bulkMealPage = 1;
        updateBulkMealPagination();
    });
    $('[data-bulk-meal-page-size]').on('change', function () {
        bulkMealPage = 1;
        updateBulkMealPagination();
    });
    updateBulkMealPagination();
    $('[data-bulk-meal-farm-price]').on('input change', function () {
        $('[data-bulk-meal-row]').each(function () {
            updateBulkMealRow($(this));
        });
        updateBulkMealTotals();
        var $activeRow = $('[data-bulk-meal-row]:visible').has(':focus');
        if ($activeRow.length) {
            updateBulkMealRowHint($activeRow.first());
        } else {
            $('[data-bulk-row-hint-top]').addClass('d-none');
        }
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
    $('[data-bulk-row-hint-top]').addClass('d-none');
    if ($('[data-bulk-meal-farm-name]').val()) {
        $('[data-bulk-meal-farm-name]').trigger('input');
    }

    // Keep tab focus within the same bulk meal row until completed
    $(document).on('keydown', '[data-bulk-meal-row] input[type="text"], [data-bulk-meal-row] input[type="number"]', function (e) {
        if (e.key !== 'Tab') {
            return;
        }

        var $row = $(this).closest('[data-bulk-meal-row]');
        if (!$row.length) {
            return;
        }

        var $inputs = $row.find('input[type="text"], input[type="number"]').filter(':visible');
        if ($inputs.length === 0) {
            return;
        }

        var currentIndex = $inputs.index(this);
        if (currentIndex === -1) {
            return;
        }

        if (e.shiftKey) {
            if (currentIndex === 0) {
                e.preventDefault();
                $inputs.last().focus();
            }
            return;
        }

        if (currentIndex === $inputs.length - 1) {
            e.preventDefault();
            $inputs.first().focus();
        }
    });

    // Delete confirmation
    $('form[data-confirm]').on('submit', function (e) {
        var message = $(this).data('confirm') || 'هل أنت متأكد من الحذف؟';
        if (!confirm(message)) {
            e.preventDefault();
        }
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
    var panehPriceIncrement = parseFloat(row.find('[data-paneh-price-increment]').val()) || 0;
    var hasPanehMeal = row.find('[data-paneh-gross-expression]').length > 0;
    var panehGrossWeight = hasPanehMeal
        ? parseNumberExpression(row.find('[data-paneh-gross-expression]').val())
        : 0;
    var panehEmptyWeight = hasPanehMeal
        ? parseNumberExpression(row.find('[data-paneh-empty-expression]').val())
        : 0;

    row.find('[data-gross-total]').text(Number.isFinite(grossWeight) ? formatNumber(grossWeight) : '-');
    row.find('[data-empty-total]').text(Number.isFinite(emptyWeight) ? formatNumber(emptyWeight) : '-');
    row.find('[data-paneh-gross-total]').text(Number.isFinite(panehGrossWeight) ? formatNumber(panehGrossWeight) : '-');
    row.find('[data-paneh-empty-total]').text(Number.isFinite(panehEmptyWeight) ? formatNumber(panehEmptyWeight) : '-');

    if (!Number.isFinite(grossWeight) || !Number.isFinite(emptyWeight) ||
        !Number.isFinite(panehGrossWeight) || !Number.isFinite(panehEmptyWeight)) {
        row.find('[data-net-weight], [data-meal-total], [data-meal-total-breakdown]').text('-');
        return;
    }

    var regularNetWeight = grossWeight - emptyWeight;
    var regularPricePerKilo = farmPrice + priceIncrement;
    var regularMealTotal = regularNetWeight * regularPricePerKilo;
    var netWeight = regularNetWeight;
    var panehMealTotal = 0;
    var panehText = '';
    if (hasPanehMeal) {
        var panehNetWeight = panehGrossWeight - panehEmptyWeight;
        var panehPricePerKilo = farmPrice + panehPriceIncrement;
        panehMealTotal = panehNetWeight * panehPricePerKilo;
        netWeight += panehNetWeight;
        row.find('[data-price-per-kilo]').text(
            'عادي: ' + formatNumber(regularPricePerKilo) +
            ' / بانيه: ' + formatNumber(panehPricePerKilo));
        panehText = ' / بانيه: ' + formatNumber(panehMealTotal);
    }

    var totalMeal = regularMealTotal + panehMealTotal;
    row.find('[data-net-weight]').text(formatNumber(netWeight));
    if (!hasPanehMeal) {
        row.find('[data-price-per-kilo]').text(formatNumber(regularPricePerKilo));
        panehText = '';
    }
    row.find('[data-meal-total]').text(formatNumber(totalMeal));
    row.find('[data-meal-total-breakdown]').text('عادي: ' + formatNumber(regularMealTotal) + panehText);
}

function updateBulkMealTotals() {
    var grossTotal = 0;
    var emptyTotal = 0;
    var netTotal = 0;
    var regularMealTotal = 0;
    var panehMealTotal = 0;
    var hasInvalidWeight = false;
    var farmPrice = parseFloat($('[data-bulk-meal-farm-price]').val()) || 0;
    var farmWeight = parseFloat($('[data-bulk-meal-farm-weight]').val()) || 0;
    var farmAddition = parseFloat($('[name="FarmAdditionAmount"]').val()) || 0;
    var farmDiscount = parseFloat($('[name="FarmDiscountAmount"]').val()) || 0;

    $('[data-bulk-meal-row]').each(function () {
        var row = $(this);
        var grossWeight = parseNumberExpression(row.find('[data-gross-expression]').val());
        var emptyWeight = parseNumberExpression(row.find('[data-empty-expression]').val());
        var hasPanehMeal = row.find('[data-paneh-gross-expression]').length > 0;
        var panehGrossWeight = hasPanehMeal
            ? parseNumberExpression(row.find('[data-paneh-gross-expression]').val())
            : 0;
        var panehEmptyWeight = hasPanehMeal
            ? parseNumberExpression(row.find('[data-paneh-empty-expression]').val())
            : 0;

        if (!Number.isFinite(grossWeight) || !Number.isFinite(emptyWeight) ||
            !Number.isFinite(panehGrossWeight) || !Number.isFinite(panehEmptyWeight)) {
            hasInvalidWeight = true;
            return;
        }

        var regularNetWeight = grossWeight - emptyWeight;
        var regularPricePerKilo = farmPrice +
            (parseFloat(row.find('[data-price-increment]').val()) || 0);

        grossTotal += grossWeight + panehGrossWeight;
        emptyTotal += emptyWeight + panehEmptyWeight;
        netTotal += regularNetWeight;
        regularMealTotal += regularNetWeight * regularPricePerKilo;
        if (hasPanehMeal) {
            var panehNetWeight = panehGrossWeight - panehEmptyWeight;
            var panehPricePerKilo = farmPrice +
                (parseFloat(row.find('[data-paneh-price-increment]').val()) || 0);
            netTotal += panehNetWeight;
            panehMealTotal += panehNetWeight * panehPricePerKilo;
        }
    });

    var mealTotal = regularMealTotal + panehMealTotal;
    $('[data-bulk-total-gross]').text(hasInvalidWeight ? '-' : formatNumber(grossTotal));
    $('[data-bulk-total-empty]').text(hasInvalidWeight ? '-' : formatNumber(emptyTotal));
    $('[data-bulk-total-net]').text(hasInvalidWeight ? '-' : formatNumber(netTotal));
    $('[data-bulk-total-meal]').text(hasInvalidWeight ? '-' : formatNumber(mealTotal));
    $('[data-bulk-total-meal-breakdown]').text(hasInvalidWeight ? 'عادي: - / بانيه: -' : 'عادي: ' + formatNumber(regularMealTotal) + ' / بانيه: ' + formatNumber(panehMealTotal));
    var farmAccount = farmWeight * farmPrice;
    $('[data-bulk-farm-account]').text(formatNumber(farmAccount));
    var previousFarmAccount = $('[data-bulk-previous-farm-account]').data('balance');
    if (Number.isFinite(previousFarmAccount)) {
        $('[data-bulk-total-farm-account]').text(
            formatNumber(previousFarmAccount + farmAccount + farmAddition - farmDiscount));
    } else if (!$('[data-bulk-meal-farm-name]').val()) {
        $('[data-bulk-total-farm-account]').text('—');
    }
    var weightDifference = netTotal - farmWeight;
    $('[data-bulk-weight-difference]').text(hasInvalidWeight ? '-' : formatNumber(weightDifference));
    $('[data-bulk-farm-profit]').text(hasInvalidWeight ? '-' : formatNumber(mealTotal - farmAccount));
    $('[data-bulk-farm-final-total]').text(hasInvalidWeight ? '-' : formatNumber(mealTotal - farmAccount));
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
    return num.toLocaleString('en-US', {
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

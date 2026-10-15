(function ($) {
    $(function () {
        var panel = document.querySelector('.bm-filter-panel');
        if (!panel) {
            return;
        }

        var viewport = window.matchMedia('(min-width: 1001px)');
        panel.open = viewport.matches;
        viewport.addEventListener('change', function (event) {
            panel.open = event.matches;
        });

        var $filters = $('.bm-filters');
        var $chips = $filters.find('.bm-active-filters');
        var $clear = $filters.find('.bm-clear-filters');
        var $slider = $('#price-range-slider');
        var clearLabel = $chips.attr('data-clear-label');

        function priceIsFiltered() {
            if (!$slider.hasClass('ui-slider')) {
                return false;
            }
            var values = $slider.slider('values');
            return values[0] !== $slider.slider('option', 'min') ||
                values[1] !== $slider.slider('option', 'max');
        }

        function resetPrice() {
            var min = $slider.slider('option', 'min');
            var max = $slider.slider('option', 'max');
            $slider.slider('values', [min, max]);
            setSelectedPriceRange(min, max);
        }

        function addChip(label, clear) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'bm-filter-chip';
            button.textContent = label;
            button.setAttribute('aria-label', clearLabel + ': ' + label);
            var icon = document.createElement('span');
            icon.setAttribute('aria-hidden', 'true');
            icon.textContent = '\u00d7';
            button.appendChild(icon);
            button.addEventListener('click', function () {
                clear();
                updateChips();
                CatalogProducts.getProducts();
            });
            $chips.append(button);
        }

        function updateChips() {
            $chips.empty();
            $filters.find('input[type="checkbox"]:checked').each(function () {
                var input = this;
                var label = input.labels.length ? input.labels[0].textContent.trim() : input.value;
                addChip(label, function () {
                    input.checked = false;
                });
            });
            if (priceIsFiltered()) {
                addChip($chips.attr('data-price-label') + ': ' + $slider.slider('values').join(' - '), resetPrice);
            }
            $clear.prop('hidden', !$chips.children().length);
        }

        $clear.on('click', function () {
            $filters.find('input[type="checkbox"]').prop('checked', false);
            if ($slider.hasClass('ui-slider')) {
                resetPrice();
            }
            updateChips();
            CatalogProducts.getProducts();
        });
        $filters.on('change', 'input[type="checkbox"]', updateChips);
        $slider.on('slidecreate slidestop', updateChips);
        $(CatalogProducts).on('loaded', updateChips);
        updateChips();
    });
})(jQuery);

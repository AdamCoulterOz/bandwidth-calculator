(() => {
    const viewportInset = 16;

    function positionTooltip(trigger) {
        const tooltip = trigger.querySelector(":scope > .context-tooltip");
        if (!tooltip) {
            return;
        }

        requestAnimationFrame(() => {
            const bounds = tooltip.getBoundingClientRect();
            const currentShift = Number.parseFloat(tooltip.style.getPropertyValue("--tooltip-shift")) || 0;
            const unshiftedLeft = bounds.left - currentShift;
            const unshiftedRight = bounds.right - currentShift;
            let shift = 0;

            if (unshiftedLeft < viewportInset) {
                shift += viewportInset - unshiftedLeft;
            }

            if (unshiftedRight + shift > window.innerWidth - viewportInset) {
                shift -= unshiftedRight + shift - (window.innerWidth - viewportInset);
            }

            tooltip.style.setProperty("--tooltip-shift", `${shift}px`);
        });
    }

    function tooltipTrigger(target) {
        if (!(target instanceof Element)) {
            return null;
        }

        return target.closest(".context-term, .aspect-lock-button, .unsupported-collapse");
    }

    document.addEventListener("pointerover", event => {
        const trigger = tooltipTrigger(event.target);
        const movedWithinTrigger = event.relatedTarget instanceof Node && trigger?.contains(event.relatedTarget);
        if (trigger && !movedWithinTrigger) {
            positionTooltip(trigger);
        }
    });

    document.addEventListener("focusin", event => {
        const trigger = tooltipTrigger(event.target);
        if (trigger) {
            positionTooltip(trigger);
        }
    });
})();

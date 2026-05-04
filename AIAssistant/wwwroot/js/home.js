window.showTextElements = function (className) {
    const elements = document.querySelectorAll(`.${className}`);

    elements.forEach((element) => {
        element.style.display = "block";
    });
};

window.AnimateTextTyping = function (className) {
    const elements = document.querySelectorAll(`.${className}`);

    elements.forEach((node) => {
        const text = node.textContent;
        const chars = text.split("");

        node.innerHTML = "";
        node.classList.add("typing");

        const addNextChar = (i) => {
            let nextChar = chars[i] === "\n" ? "<br>" : chars[i];

            node.innerHTML += "<span>" + nextChar + "</span>";

            if (i < chars.length - 1) {
                setTimeout(() => addNextChar(i + 1), 20 + Math.random() * 20);
            } else {
                setTimeout(() => node.classList.remove("typing"), 20 + Math.random() * 20);
            }
        };

        addNextChar(0);
    });
};

window.adjustHeight = function (textarea) {
    textarea.style.height = "24px";

    const maxHeight = 150;
    const newHeight = Math.min(textarea.scrollHeight, maxHeight);

    textarea.style.height = newHeight + "px";
    textarea.style.overflowY = textarea.scrollHeight > maxHeight ? "auto" : "hidden";

    const holder = textarea.parentElement;

    if (newHeight <= 28) {
        holder.style.alignItems = "center";
        holder.style.borderRadius = "50px";
    } else {
        holder.style.alignItems = "flex-start";
        holder.style.borderRadius = "28px";
    }
};

window.autoResizeTextArea = function (textarea) {
    textarea.style.height = "auto";
    textarea.style.height = textarea.scrollHeight + "px";
};

window.initializeTooltips = function () {
    if (typeof bootstrap === "undefined") {
        return;
    }

    const tooltipTriggerList = [].slice.call(
        document.querySelectorAll('[data-bs-toggle="tooltip"]')
    );

    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
};
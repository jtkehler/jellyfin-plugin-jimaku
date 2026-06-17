const JimakuConfig = {
    pluginUniqueId: '859cd24d-e976-423d-9f24-38a9f037cc0b'
};

export default function (view, params) {
    let invalidApiKeyWarning;

    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();
        const page = this;
        invalidApiKeyWarning = page.querySelector("#invalidApiKeyWarning");

        ApiClient.getPluginConfiguration(JimakuConfig.pluginUniqueId).then(function (config) {
            page.querySelector('#apikey').value = config.ApiKey || '';
            page.querySelector('#preferredkeywords').value = config.PreferredKeywords || '';
            page.querySelector('#blacklistedterms').value = config.BlacklistedTerms || '';
            if (config.ApiKeyInvalid) {
                invalidApiKeyWarning.style.display = null;
            }
            Dashboard.hideLoadingMsg();
        }).catch(function () {
            Dashboard.hideLoadingMsg();
            Dashboard.processErrorResponse({ statusText: "Failed to load plugin configuration" });
        });
    });

    view.querySelector('#JimakuConfigForm').addEventListener('submit', function (e) {
        e.preventDefault();
        Dashboard.showLoadingMsg();
        const form = this;
        ApiClient.getPluginConfiguration(JimakuConfig.pluginUniqueId).then(function (config) {
            const apiKey = form.querySelector('#apikey').value.trim();

            if (!apiKey) {
                Dashboard.hideLoadingMsg();
                Dashboard.processErrorResponse({statusText: "API key is required"});
                return;
            }

            const el = form.querySelector('#jimakuresponse');
            const saveButton = form.querySelector('#save-button');

            const data = JSON.stringify({ ApiKey: apiKey });
            const url = ApiClient.getUrl('Jellyfin.Plugin.Jimaku/ValidateApiKey');

            const handler = response => response.json().then(res => {
                saveButton.disabled = false;
                Dashboard.hideLoadingMsg();

                if (response.ok) {
                    el.innerText = 'API key validated successfully';

                    config.ApiKey = apiKey;
                    config.ApiKeyInvalid = false;
                    config.PreferredKeywords = form.querySelector('#preferredkeywords').value.trim();
                    config.BlacklistedTerms = form.querySelector('#blacklistedterms').value.trim();

                    ApiClient.updatePluginConfiguration(JimakuConfig.pluginUniqueId, config).then(function (result) {
                        invalidApiKeyWarning.style.display = 'none';
                        Dashboard.processPluginConfigurationUpdateResult(result);
                    }).catch(function () {
                        Dashboard.processErrorResponse({ statusText: "Failed to update plugin configuration" });
                    });
                } else {
                    let msg = res.Message ?? JSON.stringify(res, null, 2);
                    Dashboard.processErrorResponse({statusText: `Request failed - ${msg}`});
                }
            }).catch(function () {
                saveButton.disabled = false;
                Dashboard.hideLoadingMsg();
                Dashboard.processErrorResponse({ statusText: "Request failed. Please check your network or server." });
            });

            saveButton.disabled = true;
            ApiClient.ajax({ type: 'POST', url, data, contentType: 'application/json'}).then(handler).catch(handler);

        }).catch(function () {
            Dashboard.hideLoadingMsg();
            Dashboard.processErrorResponse({ statusText: "Failed to load plugin configuration" });
        });
        return false;
    });
}

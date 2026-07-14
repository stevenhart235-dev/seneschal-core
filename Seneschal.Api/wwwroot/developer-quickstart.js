(() => {
    const dataElement = document.getElementById('quickstartData');
    if (!dataElement) return;

    const capabilities = JSON.parse(dataElement.textContent || '[]');
    const capabilitySelect = document.getElementById('quickstartCapability');
    const tabs = [...document.querySelectorAll('[data-integration]')];
    const keyAvailable = document.getElementById('quickstartKeyAvailable');
    const keyUnavailable = document.getElementById('quickstartKeyUnavailable');
    const keyContext = document.getElementById('quickstartKeyContext');
    const maskedKey = document.getElementById('quickstartMaskedKey');
    const revealButton = document.getElementById('revealQuickstartKey');
    let integration = 'aspnet';
    let keyRevealed = false;
    let selectedKey = null;

    const documentation = {
        aspnet: 'https://github.com/stevenhart235-dev/seneschal-core/blob/main/docs/quickstart/aspnet-core-quickstart.md',
        client: 'https://github.com/stevenhart235-dev/seneschal-core/blob/main/Seneschal.Client/README.md',
        github: 'https://github.com/stevenhart235-dev/seneschal-core/blob/main/integrations/github-actions/README.md',
        terraform: 'https://github.com/stevenhart235-dev/seneschal-core/blob/main/integrations/terraform/README.md'
    };

    capabilities.forEach(capability => {
        const option = document.createElement('option');
        option.value = capability.id;
        option.textContent = `${capability.displayName} (${capability.id})`;
        capabilitySelect.appendChild(option);
    });

    const initialCapability = capabilities.find(capability =>
        capability.id === 'production.deployment.execute');
    if (initialCapability) capabilitySelect.value = initialCapability.id;

    tabs.forEach(tab => tab.addEventListener('click', () => {
        integration = tab.dataset.integration;
        tabs.forEach(item => item.setAttribute(
            'aria-selected',
            item === tab ? 'true' : 'false'));
        keyRevealed = false;
        render();
    }));
    capabilitySelect.addEventListener('change', () => {
        keyRevealed = false;
        render();
    });
    revealButton.addEventListener('click', () => {
        keyRevealed = !keyRevealed;
        renderKey();
    });

    document.querySelectorAll('.copy-snippet').forEach(button => {
        button.addEventListener('click', async () => {
            const target = document.getElementById(button.dataset.copyTarget);
            await copyText(target.textContent);
            const original = button.textContent;
            button.textContent = 'Copied';
            window.setTimeout(() => button.textContent = original, 1400);
        });
    });

    function render() {
        const capability = capabilities.find(item =>
            item.id === capabilitySelect.value) || capabilities[0];
        if (!capability) return;

        selectedKey = chooseKey(capability.keys || []);
        const identity = selectedKey?.identity || capability.policyIdentity;
        const environment = selectedKey?.environment || capability.environment;
        const resource = capability.resource;
        const snippets = createSnippets(
            capability.id,
            identity,
            environment,
            resource);

        document.getElementById('quickstartInstall').textContent = snippets.install;
        document.getElementById('quickstartConfiguration').textContent = snippets.configuration;
        document.getElementById('quickstartExample').textContent = snippets.example;
        document.getElementById('quickstartDocsLink').href = documentation[integration];
        renderKey();
    }

    function chooseKey(keys) {
        const preferred = {
            aspnet: ['sample-protected-api'],
            client: [],
            github: ['github-actions'],
            terraform: ['terraform']
        }[integration];
        return keys.find(key => preferred.some(term => key.name.includes(term))) ||
            keys[0] ||
            null;
    }

    function renderKey() {
        keyAvailable.hidden = !selectedKey;
        keyUnavailable.hidden = Boolean(selectedKey);
        if (!selectedKey) {
            keyContext.textContent =
                'Use a separately configured key scoped to the selected identity and capability.';
            return;
        }

        keyContext.textContent =
            `${selectedKey.name} · identity ${selectedKey.identity} · environment ${selectedKey.environment}`;
        maskedKey.textContent = keyRevealed
            ? selectedKey.value
            : '••••••••••••••••••••••••';
        revealButton.textContent = keyRevealed ? 'Hide' : 'Reveal';
        revealButton.setAttribute('aria-pressed', keyRevealed ? 'true' : 'false');
    }

    function createSnippets(capability, identity, environment, resource) {
        const safeIdentity = identity || '<configured-identity>';
        const safeResource = resource || '<resource-id>';
        const commonArguments = `  -BaseUrl http://localhost:5000 \`\n` +
            `  -ApiKey $env:SENESCHAL_API_KEY \`\n` +
            `  -Identity ${safeIdentity} \`\n` +
            `  -Capability ${capability} \`\n` +
            `  -Environment ${environment} \`\n` +
            `  -Resource ${safeResource}`;

        if (integration === 'aspnet') {
            return {
                install: 'dotnet add package Seneschal.AspNetCore --version 0.1.0-alpha.1',
                configuration: `builder.Services.AddSeneschal(options =>\n{\n    options.BaseUrl = new Uri("http://localhost:5000");\n    options.ApiKey = builder.Configuration["Seneschal:ApiKey"]!;\n    options.IdentityResolver = _ => "${safeIdentity}";\n    options.DefaultEnvironment = "${environment}";\n});\n\napp.UseSeneschal();`,
                example: `app.MapPost("/governed-operation", () =>\n        Results.Ok(new { executed = true }))\n    .RequireCapability("${capability}");`
            };
        }

        if (integration === 'client') {
            return {
                install: 'dotnet add package Seneschal.Client --version 0.1.0-alpha.1',
                configuration: `builder.Services.Configure<SeneschalClientOptions>(options =>\n{\n    options.BaseUrl = new Uri("http://localhost:5000");\n    options.ApiKey = builder.Configuration["Seneschal:ApiKey"];\n});\nbuilder.Services.AddHttpClient<ISeneschalClient, SeneschalClient>();`,
                example: `var result = await client.EvaluateAsync(new DecisionRequest\n{\n    Identity = "${safeIdentity}",\n    Capability = "${capability}",\n    Context = new()\n    {\n        ["environment"] = "${environment}",\n        ["resource"] = "${safeResource}"\n    }\n}, cancellationToken);\n\nif (result.ShouldProceed)\n{\n    await ExecuteAsync(cancellationToken);\n}`
            };
        }

        if (integration === 'github') {
            return {
                install: 'Copy integrations/github-actions/invoke-seneschal-gate.ps1 into the workflow workspace.',
                configuration: `Repository secrets:\nSENESCHAL_URL=http://localhost:5000\nSENESCHAL_API_KEY=<scoped secret>`,
                example: `powershell -File integrations/github-actions/invoke-seneschal-gate.ps1 \`\n${commonArguments}`
            };
        }

        return {
            install: 'terraform -chdir=integrations/terraform/examples/production-apply init\n# Or replace terraform with tofu.',
            configuration: `terraform -chdir=integrations/terraform/examples/production-apply plan -out=tfplan\n$env:SENESCHAL_API_KEY = '<scoped secret>'`,
            example: `powershell -File integrations/terraform/invoke-seneschal-gate.ps1 \`\n${commonArguments} \`\n  -PlanFile integrations/terraform/examples/production-apply/tfplan\n\nif ($LASTEXITCODE -eq 0) {\n  terraform -chdir=integrations/terraform/examples/production-apply apply tfplan\n}`
        };
    }

    async function copyText(value) {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(value);
            return;
        }
        const textarea = document.createElement('textarea');
        textarea.value = value;
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.appendChild(textarea);
        textarea.select();
        document.execCommand('copy');
        textarea.remove();
    }

    render();
})();

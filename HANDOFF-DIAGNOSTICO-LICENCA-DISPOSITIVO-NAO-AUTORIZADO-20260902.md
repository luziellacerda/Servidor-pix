# HANDOFF — DIAGNÓSTICO DE LICENÇA/DISPOSITIVO NÃO AUTORIZADO

## Sintoma

O cliente R25 exibe: `LICENÇA, CÓDIGO OU DISPOSITIVO NÃO AUTORIZADO`.

## Causa identificada

O servidor mantém a licença ativa, mas compara o vínculo da máquina campo a campo. O novo executável está enviando uma identidade diferente da identidade cadastrada ou não está carregando o estado local original.

## Campos que precisam coincidir

- `LicenseId`
- `DeviceId`
- `PublicKeySpki`
- `HardwareFingerprint`
- `BindingType`
- `Algorithm`

O servidor rejeita com `DEVICE_DENIED` quando qualquer um diverge. A chave pública da nova autoridade de conteúdo não altera licenças nem vínculos de máquina.

## Ação obrigatória no cliente

1. Confirmar que o `.exe` usa o `LicenseId` ativo correto.
2. Preservar/carregar o armazenamento de identidade local existente; não gerar nova chave da máquina durante atualização.
3. Comparar o `DeviceId`, `PublicKeySpki` e `HardwareFingerprint` enviados com o cadastro do servidor.
4. Não recriar licença e não alterar o banco do servidor.
5. Exibir o código real de recusa apenas em log local seguro para diagnóstico, sem expor chaves privadas.
6. Após corrigir o carregamento da identidade, executar ativação/sessão e teste E2E.

## Segurança

- Não desabilitar as comparações do servidor.
- Não aceitar qualquer fingerprint para contornar a recusa.
- Não copiar nem enviar chaves privadas.
- A autoridade de conteúdo rotacionada é independente do vínculo de licença.

## Critério de conclusão

O mesmo PC deve abrir com o vínculo já cadastrado, sem recriar licença, mantendo todas as validações de identidade e segurança.

# HANDOFF PARA IA — integrar o programa ao licenciamento TurboRama

## Instrução principal para a IA executora

Você está recebendo a tarefa de integrar um programa cliente ao servidor de licenciamento TurboRama
já existente. Não recrie o servidor do zero e não invente um protocolo paralelo. Primeiro faça o
inventário do programa cliente, identifique linguagem, plataforma, instalador, login atual, mecanismo
de download, atualização e armazenamento local. Depois implemente a integração por etapas, com testes
automatizados e sem remover funcionalidades existentes.

O resultado esperado é:

- a instalação precisa de licença e código de ativação criado no servidor;
- a licença fica vinculada criptograficamente a um computador;
- copiar o EXE ou os arquivos para outro computador não concede acesso;
- o servidor mostra computador, estado e último contato;
- tentativas feitas por outro computador ficam registradas;
- o administrador pode suspender, revogar ou transferir a licença;
- downloads protegidos somente são liberados para instalação autenticada;
- o programa suporta indisponibilidade temporária da internet com uma autorização offline curta;
- nenhuma chave mestra, senha administrativa ou segredo do servidor entra no programa cliente.

## 1. Contexto do servidor existente

Servidor produtivo:

```text
API: https://pix.lzgames.com.br
Painel: https://painelpix.lzgames.com.br/admin
Schema do protocolo: 1
Algoritmo de prova: RSA-PSS-SHA256
Domínio de assinatura: TurboRamaOnlineMachineProof/v1
```

Código de referência no servidor:

```text
/home/lz-servidor/turborama-download/Servidor-pix/src/TurboRamaPixOnlineServer/OnlineLicenseProtocol.cs
/home/lz-servidor/turborama-download/Servidor-pix/src/TurboRamaPixOnlineServer/ServerCore.cs
/home/lz-servidor/turborama-download/Servidor-pix/src/TurboRamaPixOnlineServer/Program.cs
```

Leia `OnlineLicenseProtocol.cs` integralmente. A serialização canônica, os hashes de contexto e a
montagem da mensagem assinada precisam ser reproduzidos byte a byte. Não use serialização JSON comum
como substituta quando a assinatura depender da ordem e formato canônicos.

Rotas existentes:

| Método | Rota | Uso |
|---|---|---|
| GET | `/v1/health` | prontidão do servidor |
| POST | `/v1/activations/challenge` | iniciar ativação |
| POST | `/v1/activations/complete` | concluir ativação com prova |
| POST | `/v1/challenges` | criar desafio para operação/sessão |
| POST | `/v1/sessions` | autenticar sessão do programa |
| POST | `/v1/orders` | rota legada de ordem |
| POST | `/v1/orders/status` | rota legada de consulta |
| POST | `/v1/configuration/read` | ler configuração autorizada |
| POST | `/v1/configuration/write` | gravar configuração autorizada |

Perfis existentes:

- `TPM_BOUND`: preferencial quando houver TPM adequado;
- `SOFTWARE_BOUND_ONLINE`: chave protegida pelo sistema operacional e verificação online frequente;
- `USB_TOKEN_BOUND`: reservado e proibido até existir token criptográfico real homologado. Pendrive
  comum não é token seguro.

## 2. Princípios obrigatórios

1. A chave privada nasce no computador cliente e nunca é enviada ao servidor.
2. O servidor armazena somente a chave pública e os metadados necessários.
3. `deviceId` é o SHA-256 hexadecimal lowercase do SPKI DER da chave pública.
4. Chave RSA entre 2048 e 4096 bits; preferir 3072 bits quando compatível.
5. Toda prova usa RSA-PSS com SHA-256.
6. Cada operação usa desafio/nonce novo, expiração curta e proteção contra repetição.
7. A assinatura cobre licença, dispositivo, sessão, ação e hash canônico do contexto.
8. O cliente nunca aceita resposta de outro hostname, HTTP simples ou certificado inválido.
9. Falha de rede é diferente de recusa explícita do servidor.
10. Uma tentativa de clonagem não deve bloquear automaticamente a máquina legítima; deve ser
    registrada e apresentada ao administrador.
11. Nenhuma decisão de segurança depende somente de um identificador de hardware fácil de copiar.
12. Não gravar segredo, código de ativação, token de download ou chave privada em log.

## 3. Fluxo de instalação e ativação

### 3.1 Preparação administrativa

O administrador cria no painel:

- cliente;
- licença;
- perfil de proteção;
- limite de máquinas;
- código de ativação único e temporário.

O código não é uma licença permanente. Ele serve apenas para autorizar o primeiro vínculo e deve ser
consumido uma única vez.

### 3.2 Primeira execução

O programa precisa apresentar uma tela de ativação com:

- identificação da licença;
- código único;
- estado da conexão;
- mensagens de erro amigáveis com código técnico;
- opção de tentar novamente sem gerar outra identidade.

Na primeira execução:

1. verificar se já existe identidade válida no armazenamento seguro;
2. se não existir, gerar o par RSA;
3. proteger a chave privada com TPM quando disponível ou armazenamento seguro do SO;
4. exportar somente a chave pública como SPKI DER/Base64;
5. calcular `deviceId` a partir do SPKI;
6. montar `OnlineDeviceDescriptor`;
7. chamar `/v1/activations/challenge`;
8. construir a mensagem canônica exatamente como o servidor;
9. assinar usando RSA-PSS-SHA256;
10. chamar `/v1/activations/complete`;
11. persistir somente o resultado necessário e nunca o código já consumido;
12. iniciar a primeira sessão autenticada.

Campos do descritor:

```text
schemaVersion
deviceId
bindingType
algorithm
publicKeySpki
hardwareFingerprint
agentVersion
```

Não gere nova chave silenciosamente quando a chave existente não puder ser aberta. Isso faria uma
instalação legítima parecer outro computador. Exiba recuperação/transferência e preserve evidências.

## 4. Identidade do computador

A segurança principal é a chave privada. O fingerprint de hardware é sinal adicional para detectar
mudanças e clonagem.

O fingerprint deve:

- ser determinístico na mesma máquina;
- normalizar valores antes do hash;
- combinar sinais estáveis disponíveis na plataforma;
- não enviar números seriais crus se um hash específico for suficiente;
- tolerar alterações pequenas, como memória ou disco secundário, conforme política;
- considerar troca de placa-mãe/TPM como transferência administrativa.

No Windows, preferir chave não exportável em TPM/CNG. Se TPM não estiver disponível, usar chave
protegida por DPAPI para a conta/máquina e marcar o perfil como `SOFTWARE_BOUND_ONLINE`.

Não use MAC address, nome do computador ou UUID isolado como identidade definitiva.

## 5. Login e sessão do programa

O login atual do usuário e a licença de máquina são camadas diferentes:

```text
Usuário/senha válidos
        +
Máquina licenciada e prova criptográfica válida
        +
Licença ativa no servidor
        =
Sessão liberada
```

Depois do login:

1. gerar `sessionId` criptograficamente aleatório;
2. montar `OnlineSessionContext` com fingerprint e versão do agente;
3. calcular o hash canônico do contexto;
4. solicitar `/v1/challenges` com ação de sessão esperada pelo contrato;
5. assinar o desafio;
6. enviar `OnlineSessionProof` a `/v1/sessions`;
7. liberar a interface somente após resposta autenticada e válida.

Nunca trate apenas `HTTP 200` como autorização. Validar schema, estado e campos obrigatórios. Falhar
fechado em resposta malformada, assinatura/contexto divergente ou recusa explícita.

## 6. Presença online e painel

Implementar heartbeat autenticado sem criar tempestade de requisições:

- ao iniciar o programa;
- após login;
- a cada 5 a 15 minutos enquanto estiver em uso;
- antes de download ou operação protegida;
- ao retornar de suspensão/rede offline.

Usar backoff exponencial com jitter em falha de rede. Não repetir automaticamente uma mutação cujo
resultado seja desconhecido. O painel deve receber, pelo mecanismo de sessão existente ou extensão
compatível do servidor:

- versão do programa;
- última conexão;
- fingerprint normalizado/hash;
- estado de sessão;
- tentativas recusadas e motivo técnico sem dados sensíveis.

Se o contrato atual não atualizar presença com a frequência necessária, ampliar servidor e cliente
na mesma alteração versionada. Não reutilizar uma rota administrativa e não criar endpoint sem
autenticação criptográfica.

## 7. Política offline

Não implementar “ativar uma vez e funcionar para sempre”. Isso impediria revogação remota.

Adicionar uma autorização offline assinada pelo servidor, com estrutura mínima:

```text
schemaVersion
licenseId
deviceId
bindingType
issuedAt
notBefore
expiresAt
policyVersion
entitlementsHash
nonce/tokenId
serverSignature
```

Política inicial recomendada:

- verificar online sempre que houver conexão;
- lease offline máximo de 72 horas;
- renovar antes de faltar 24 horas;
- tolerância curta para mudança de relógio, sem aceitar retorno arbitrário no tempo;
- após expirar, manter apenas tela de ativação/recuperação e bloquear recursos protegidos;
- recusa explícita `SUSPENDED`, `REVOKED`, dispositivo bloqueado ou reautenticação obrigatória invalida
  o lease local assim que recebida;
- falha temporária de DNS, timeout ou `5xx` não equivale a revogação imediata enquanto o lease válido
  existir.

A assinatura do lease deve usar uma chave pública do servidor embutida/pinada no cliente. A chave
privada de assinatura permanece exclusivamente no servidor/cofre. Planejar rotação com `keyId` e
suporte simultâneo à chave atual e próxima.

Armazenar o lease com proteção contra edição e rollback. O programa deve validar assinatura,
`deviceId`, validade, schema e política em toda inicialização e antes de recursos sensíveis.

## 8. Suspensão, revogação e transferência

Estados mínimos:

- `ACTIVE`;
- `SUSPENDED`;
- `MAINTENANCE`;
- `TRANSFER_PENDING`;
- `REVOKED`.

Comportamento:

- `ACTIVE`: operação normal;
- `SUSPENDED`: bloquear novos downloads e operações protegidas; mostrar contato/suporte;
- `MAINTENANCE`: aplicar política definida, normalmente acesso limitado;
- `TRANSFER_PENDING`: máquina antiga deixa de criar novas sessões e a nova usa código único;
- `REVOKED`: negar funcionamento protegido e apagar tokens temporários, preservando logs locais
  sanitizados.

Troca legítima de PC ou placa-mãe deve usar a ação de transferência do painel. Não oriente o usuário
a apagar arquivos, editar registro ou gerar chaves repetidamente.

## 9. Proteção dos downloads

Não manter URLs permanentes, tokens de armazenamento ou segredos dentro do EXE. Ofuscação não torna
um segredo seguro.

Arquitetura requerida:

```text
Cliente prova licença e sessão
        ↓
Solicita autorização para artefato específico
        ↓
Servidor valida licença, dispositivo e entitlement
        ↓
Servidor gera ticket/URL de uso único e curta duração
        ↓
Cliente baixa, valida hash e assinatura
        ↓
Ticket expira ou é consumido
```

Se o servidor atual ainda não tiver endpoints de download, implementar uma extensão versionada, por
exemplo:

```text
POST /v1/downloads/challenge
POST /v1/downloads/authorize
GET  /v1/downloads/{ticket}
```

O ticket deve ser:

- aleatório com pelo menos 128 bits de entropia;
- armazenado apenas como hash no servidor;
- vinculado a licença, `deviceId`, sessão e artefato;
- uso único ou com limite estrito de retomada;
- válido por poucos minutos;
- incapaz de listar caminhos internos;
- auditado sem registrar o token completo.

Cada manifesto de download deve conter nome lógico, versão, tamanho, SHA-256 e assinatura do
publicador. Baixar em arquivo temporário, validar antes de mover atomicamente para o destino e apagar
arquivo incompleto em falha.

Se hoje os downloads estiverem integralmente embutidos no EXE, a IA deve primeiro inventariar isso e
propor migração compatível. Não remover o mecanismo antigo antes de o novo servidor, cliente e rollback
estarem testados.

## 10. Armazenamento local seguro

Separar:

- identidade privada da máquina;
- metadados públicos da licença;
- lease offline;
- cache de download;
- preferências não sensíveis;
- logs sanitizados.

No Windows:

- TPM/CNG para chave não exportável quando possível;
- DPAPI `LocalMachine` ou escopo apropriado como fallback;
- ACL permitindo apenas conta administrativa/serviço necessário;
- nada secreto no Registro sem proteção criptográfica;
- instalador não deve imprimir segredo em log MSI/console.

Não permitir exportação de chave privada pela interface. Backup de identidade deve ser uma decisão de
produto separada e, se existir, exigir envelope criptográfico e autorização do servidor.

## 11. Atualizações e integridade do programa

- assinar executável e instalador com certificado de assinatura de código;
- assinar manifestos de atualização;
- verificar assinatura e hash antes de executar atualização;
- bloquear downgrade abaixo de versão mínima determinada pelo servidor;
- usar atualização atômica com rollback;
- não considerar hash local do EXE uma proteção suficiente contra patch;
- mover decisões críticas e liberações de conteúdo para o servidor.

Ofuscação pode aumentar o custo da engenharia reversa, mas não substitui criptografia, autenticação e
controle no servidor.

## 12. Tratamento de erros

A interface deve diferenciar:

- internet indisponível com lease ainda válido;
- lease expirado;
- código inválido ou consumido;
- licença suspensa/revogada;
- dispositivo diferente;
- transferência pendente;
- relógio inválido;
- servidor temporariamente indisponível;
- resposta/prova criptográfica inválida;
- versão mínima obrigatória.

Mostrar mensagem simples ao usuário e código técnico estável. Logs não podem conter senha, código de
ativação, chave privada, assinatura completa, ticket de download ou corpo sensível.

## 13. Resistência a clonagem e abuso

Implementar:

- nonce de uso único;
- expiração curta dos desafios;
- `sessionId` aleatório;
- idempotência para mutações;
- rate limit cliente e servidor;
- detecção de mesma licença em identidades diferentes;
- sessão exclusiva conforme contrato existente;
- auditoria da origem e motivo da recusa;
- limitação de tentativa de código;
- comparação em tempo constante onde aplicável;
- limpeza explícita de buffers de chave quando a plataforma permitir.

Não bloquear automaticamente a licença legítima por uma única tentativa suspeita. Registrar, alertar e
permitir ação administrativa. Automatismos devem exigir limiar, janela temporal e recuperação segura.

## 14. Fases de implementação

### Fase 0 — inventário obrigatório

- identificar repositório, branch e mudanças locais do programa;
- mapear login, inicialização, instalador e downloads;
- localizar segredos/URLs embutidos sem exibi-los;
- identificar frameworks criptográficos disponíveis;
- registrar compatibilidade de Windows e presença de TPM;
- criar plano de migração e rollback.

### Fase 1 — biblioteca de protocolo

- portar tipos e serialização canônica;
- gerar/importar chave RSA;
- calcular `deviceId`, hashes de contexto e mensagens de assinatura;
- criar cliente HTTPS resiliente;
- escrever vetores de teste cruzados com o servidor.

### Fase 2 — ativação

- tela de ativação;
- armazenamento seguro;
- challenge/complete;
- estados e recuperação;
- teste com código falso em ambiente isolado.

### Fase 3 — sessão e presença

- integrar após login;
- heartbeat e backoff;
- exibição online no painel;
- suspensão/revogação;
- transferência de hardware.

### Fase 4 — lease offline

- contrato assinado e rotação de chave;
- cache protegido;
- expiração, relógio e renovação;
- políticas de falha de rede versus recusa explícita.

### Fase 5 — downloads protegidos

- inventário e catálogo de artefatos;
- endpoint autenticado/tickets;
- hash, assinatura, expiração e auditoria;
- migração dos links/arquivos legados.

### Fase 6 — endurecimento e rollout

- assinatura de código;
- ofuscação opcional;
- telemetria sanitizada;
- piloto com uma licença;
- rollback testado;
- expansão gradual.

Não pular diretamente para produção.

## 15. Testes de aceitação obrigatórios

### Ativação

- código válido ativa exatamente uma vez;
- repetir o código é recusado;
- assinatura errada é recusada;
- `deviceId` divergente da chave pública é recusado;
- chave RSA fraca/malformada é recusada;
- desafio expirado ou repetido é recusado.

### Vínculo

- copiar EXE/configuração pública para outro PC não autentica;
- copiar todos os arquivos sem a chave privada não autentica;
- chave privada de outro PC não autentica;
- pequena mudança permitida de hardware mantém política esperada;
- troca de placa-mãe exige transferência quando aplicável.

### Sessão e painel

- programa legítimo aparece online;
- último contato é atualizado;
- duas sessões incompatíveis seguem a política exclusiva;
- tentativa clonada aparece na auditoria sem derrubar automaticamente o original;
- suspensão bloqueia novas operações;
- reativação administrativa funciona sem reinstalação;
- revogação invalida próxima conexão/renovação.

### Offline

- sem internet e lease válido funciona pelo período permitido;
- lease expirado bloqueia recursos protegidos;
- edição de data não estende licença indefinidamente;
- lease de outro dispositivo é recusado;
- assinatura alterada é recusada;
- timeout/`5xx` não é interpretado como revogação enquanto houver lease válido.

### Downloads

- licença ativa baixa artefato autorizado;
- suspensa/revogada não recebe ticket;
- ticket de outro dispositivo falha;
- ticket expirado ou reutilizado falha;
- arquivo adulterado falha no hash/assinatura;
- URL interna permanente não aparece no binário ou logs;
- download interrompido não substitui arquivo válido.

### Segurança

- MITM/certificado inválido falha fechado;
- HTTP simples é recusado;
- segredo não aparece em log, crash dump comum ou argumentos de processo;
- instalador preserva ACLs;
- atualização adulterada é recusada;
- servidor indisponível não causa loop agressivo de requisições.

## 16. Critérios de conclusão

A tarefa só pode ser declarada concluída quando houver:

1. integração real com o servidor existente;
2. código revisável e testes automatizados;
3. vetores criptográficos compatíveis cliente/servidor;
4. ativação e sessão demonstradas em ambiente de teste;
5. clonagem para segunda máquina comprovadamente recusada;
6. presença e tentativa recusada visíveis no painel;
7. suspensão, reativação, revogação e transferência testadas;
8. lease offline assinado e com expiração testada;
9. downloads protegidos por autorização temporária;
10. instalador e atualização com rollback;
11. documentação operacional sem segredos;
12. nenhum dado real, pagamento real ou licença de cliente alterado durante testes.

## 17. Entregáveis exigidos da IA

- relatório inicial do inventário;
- plano de implementação por arquivos/componentes;
- código cliente e, se indispensável, extensão versionada do servidor;
- testes unitários, integração e ponta a ponta;
- vetores criptográficos cliente/servidor;
- migração do mecanismo antigo;
- instalador atualizado;
- runbook de deploy e rollback;
- relatório final com evidências e limitações;
- lista explícita de pendências que dependem do proprietário.

## 18. Proibições para a IA executora

- não expor ou copiar segredos do servidor;
- não embutir senha administrativa ou chave mestra no programa;
- não desativar TLS ou validação de certificado;
- não criar licença/pagamento real para teste;
- não apagar login ou mecanismo atual antes da migração aprovada;
- não usar identificador simples de hardware como única segurança;
- não prometer que o EXE será “impossível de hackear”;
- não tornar o programa eternamente offline após uma única ativação;
- não alterar Nginx, Cloudflare, banco ou outros serviços fora do escopo;
- não fazer deploy automático em produção sem backup e autorização;
- não descartar mudanças locais existentes do usuário.

## 19. Decisões que precisam do proprietário

A IA deve solicitar decisão antes de finalizar:

- sistemas operacionais e versões mínimas suportadas;
- prazo offline desejado (recomendação inicial: 72 horas);
- TPM obrigatório ou fallback por software;
- quantidade de computadores por licença;
- política para manutenção e suspensão;
- quais downloads/recursos exigem autorização;
- onde os artefatos protegidos serão armazenados;
- canal de alerta (painel, WhatsApp, e-mail);
- política de privacidade e retenção de IP/auditoria;
- certificado de assinatura de código e processo de atualização.

## 20. Observação de segurança realista

Um executável no computador do cliente pode ser modificado por um invasor com tempo e acesso. O
objetivo não é confiar no EXE isoladamente. O controle efetivo vem de chave privada vinculada ao
dispositivo, desafios criptográficos, lease curto, verificação periódica, recursos indispensáveis
liberados pelo servidor e downloads temporários assinados.

Uma cópia totalmente modificada, que contenha todos os recursos e opere para sempre offline, não pode
ser desligada remotamente. Por isso, não deixar dentro do EXE tudo o que torna o produto útil sem o
servidor.

---

Antes de começar, leia também:

```text
/home/lz-servidor/turborama-download/Servidor-pix/HANDOFF-COMPLETO-TURBORAMA-SERVIDOR-2026-08-21.md
/home/lz-servidor/turborama-download/Servidor-pix/README.md
/home/lz-servidor/turborama-download/Servidor-pix/HANDOFF-LINUX-SEGURANCA-TRANSPORTE-RODADA-13.md
```

Este handoff define o resultado e as restrições. A IA executora deve adaptar a implementação à
tecnologia real do programa cliente depois do inventário, sem enfraquecer o protocolo existente.

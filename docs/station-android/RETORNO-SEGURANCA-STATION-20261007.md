# Segurança Station — correção de 07/10/2026

## Estado desta revisão

Fonte preparada para publicação, com backup/restauração, ensaio com a identidade definitiva e retorno automático antes da troca. A evidência de produção será adicionada depois das verificações. Base API publicada: a3e83d96, DLL 5fff55c1; código anterior 8d9c670. Base Android: 029612b, R55 + visual R57 + prontidão + palavra passe automática.

## Correções

- API Station com usuário Linux e papel PostgreSQL próprios; visões filtradas pelo produto e função de vínculo restrita. Sem credenciais, chave privada ou pepper da Suite dentro do processo Station.
- Processo com arquivos de sistema somente para leitura, diretórios dos outros sistemas inacessíveis, mídias compiladas permitidas e leitura das importações futuras por ACL. Chaves Station existentes preservadas.
- Rotas Station atendidas pelo túnel local; acesso direto à origem recusado, inclusive com cabeçalho Cloudflare forjado. Outros produtos conservam as rotas atuais. Cabeçalho de prova encaminhado e limite de 64 KiB somente nos dois envelopes de autenticação.
- Pedidos autenticados e abertura WSS assinados pelo aparelho: método, rota, corpo, credencial, horário e nonce. Token ou ticket copiado e repetição são recusados. Depois da adesão assinada de um aparelho, uma sessão mais fraca é recusada. Nenhuma assinatura por frame ou nova verificação de integridade de ROM.
- Verificação opcional de chave Android atestada: raízes oficiais Google, revogação, pacote org.turboramastation.frontend, certificado original, boot verificado e propriedade da chave. Política global estrita desligada até qualificação dos aparelhos e atualização do APK.
- Compartilhamento Samba obsoleto específico passa a exigir autenticação; os outros compartilhamentos são preservados. Seu diretório está ausente: a simulação não demonstrou vazamento por esse compartilhamento.
- Catálogo com metadata=1 passa a enviar objeto vazio quando não existe sinopse; o cliente anterior não aceita metadata:null.

## Compatibilidade e limites

Migrations 031/032 aditivas, sem apagar licenças ou conteúdo. O retorno restaura identidade, configuração, ACL, proxy e executável anteriores e desativa o novo papel, preservando os dados que chegaram. Não se restaura o banco de produção sobre novas vendas. Scripts exigem DLL, catálogo, registro dos quatro motores, fonte limpa e ausência de partida ativa na troca; recusam sucessoras.

APKs antigos continuam utilizando seu protocolo atual. A prova por pedido protege somente os aparelhos que aderiram pelo APK atualizado. Enquanto Station:Security:RequireVerifiedApp=false, uma ativação válida por cliente próprio ainda é permitida: não afirmar exclusividade de APK nem segurança absoluta. Cópia de código válido não usado continua sendo uma credencial; manter a emissão de uso único/30 minutos e o vínculo existente.

A política de cadeia é estrita com validade de certificados. Aparelhos com certificados antigos expirados ou sem hardware atestado usam prova RSA compatível enquanto a política global estiver desligada; a raiz e a revogação continuam obrigatórias para o selo verifiedApp. Nunca forçar a política antes de testar os modelos reais. Chave RSA principal/identidade atual não é substituída. Recuperação de aparelho já protegido exige cliente atualizado ou retorno coordenado do backend, sem limpar dados ou desinstalar o app.

Firewall global, SSH, PIX, Suite Windows, ES Windows, site, WhatsApp, painel, bancos e gateway preservados. Não há motivo confirmado para fechar globalmente as portas compartilhadas. Capacidade do relay 512 salas/1024 conexões mantida; não comprova gameplay em centenas de celulares.

## Evidência concluída antes da publicação

- Build .NET Release: zero erros/avisos; suíte completa incluindo contratos estritos, concorrência, produto e segurança criptográfica passou.
- PostgreSQL temporário: seis operações proibidas negadas ao novo papel; ativação, emissão, suspensão, transferência, revogação e limpeza sintéticas passaram.
- 28 verificações HTTP/WSS de proteção, bytes nas duas direções, rota/corpo/token/chave alterados, replay, downgrade e política estrita temporária passaram.
- Cliente Java real contra API candidata: ativação, renovação, catálogo, perfil, quatro capas simultâneas, download exato e prova de relay; 21 pedidos protegidos passaram.
- 190 fontes Java8/API34 compilaram; regressões cliente: 75 API, 13 reutilização de transferência, 41 concorrência de capas e 180 publicação de capas passaram.
- Verificador de publicação exercitado no PostgreSQL temporário; catálogo e limpeza passaram. Transformações preservam outras rotas/compartilhamentos e transferências sem buffering/900 segundos.

## Aplicativo e próximas etapas de produção Android

Delta em versions/station-security-r57-20261007 no TurboElden. Compor as fontes preservadas R55 + visual R57 + acesso automático + proteção. Compilar cliente/DEX28 e salas/DEX35 juntos, usando o novo cliente como classpath das salas. Empacotar apenas sobre APK R57 e6159fa3 com certificado original 7b16ee1a, runtime auto-password 899e3527 e seu engines.json. Conferir todos os outros arquivos, alinhamento 16 KiB, classes sem duplicação e atualização sem limpar dados.

O Linux não tem o APK privado, D8 e assinatura do PC de produção: novo DEX/APK, instalação, cadeia hardware e partida física continuam pendentes. Testar os dois aparelhos com quatro capas/download/renovação, ambos nomes na sala, Pronto, entrada automática, inputs e retorno, além dos casos de credencial copiada. Registrar versão/pacote/certificado e signed verifiedApp. Exigir APK original globalmente somente após todos os clientes necessários estarem qualificados ou existir política de migração operacional.

Referências: [Android key attestation](https://developer.android.com/privacy-and-security/security-key-attestation), [formato de atestação](https://source.android.com/docs/security/features/keystore/attestation), [proteção da origem Cloudflare](https://developers.cloudflare.com/fundamentals/security/protect-your-origin-server/). O verificador específico usa validade estrita e não substitui a qualificação de certificados antigos recomendada pelo Android.

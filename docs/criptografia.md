# Criptografia — BillingEngine

> **Status:** Ativo · **Versão:** 1.0 · **Última revisão:** 2026-10-08

---

## 1. Objetivo

Descrever a implementação de criptografia simétrica para proteção de dados sensíveis em
repouso: segredos TOTP e documentos de clientes (CPF/CNPJ).

---

## 2. Justificativa Técnica

### Por que AES-256-GCM e não AES-CBC?

| Critério              | AES-CBC                             | AES-256-GCM ✓                           |
|-----------------------|-------------------------------------|------------------------------------------|
| Autenticação          | Não — vulnerável a padding oracle   | Sim — AEAD (detecta adulteração)         |
| Nonce/IV reutilizável | IV fixo → padrões detectáveis       | Nonce novo por operação via CSPRNG       |
| Performance           | Mais lento em hardware moderno      | Acelerado por AES-NI nas CPUs            |
| Padrão NIST           | SP 800-38A (mais antigo)            | SP 800-38D (recomendação atual)          |

**ADR-003** (documentado no README): AES-256-GCM escolhido por AEAD + performance.

---

## 3. Arquitetura

### Componentes

```
IEncryptionService (Application.Interfaces)
       ↑ implementado por
AesEncryptionService (Infrastructure.Security)
       │ usa
AesGcm (.NET BCL — System.Security.Cryptography)
       │ chave via
IConfiguration ["Encryption:Key"]  ←── variável de ambiente Encryption__Key
```

### Formato do Blob Criptografado

```
┌─────────────────────────────────────────────────────────────────┐
│  nonce (12 bytes)  │  ciphertext (N bytes)  │  tag (16 bytes)  │
└─────────────────────────────────────────────────────────────────┘
                   codificado em Base64 para armazenamento
```

- **Nonce (12 bytes / 96 bits):** gerado aleatoriamente via `RandomNumberGenerator` para cada operação
- **Ciphertext:** texto cifrado (mesmo tamanho do plaintext)
- **Tag (16 bytes / 128 bits):** tag de autenticação — garante integridade e autenticidade

---

## 4. Regras de Negócio

1. A mesma chave AES cifra todos os campos sensíveis — rotação de chave requer re-cifragem (Passo 6)
2. Nunca armazenar o plaintext de TOTP secret ou documentos no banco
3. O `AesEncryptionService` lança no construtor se a chave não estiver configurada ou tiver tamanho errado
4. `Decrypt` lança `CryptographicException` com mensagem genérica — nunca revela detalhes ao cliente
5. Ciphertext adulterado lança `AuthenticationTagMismatchException` antes de retornar dados

---

## 5. Fluxo de Funcionamento

### Encrypt

```
plaintext (string UTF-8)
    │
    ├─ GetBytes(NonceSizeBytes=12) ──> nonce aleatório (CSPRNG)
    │
    ├─ AesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag)
    │
    └─ Convert.ToBase64String( nonce || ciphertext || tag )
              └── retorna string armazenável em banco de dados
```

### Decrypt

```
base64 (string)
    │
    ├─ Convert.FromBase64String(...)
    │
    ├─ Extrai nonce[0..11], ciphertext[12..N-17], tag[N-16..N-1]
    │
    ├─ AesGcm.Decrypt(nonce, ciphertext, tag, plaintext)
    │     └── lança se tag inválida (adulteração detectada)
    │
    └─ Encoding.UTF8.GetString(plaintext) ──> string original
```

---

## 6. Configurações

```json
"Encryption": {
  "Key": "<base64 de 32 bytes>"
}
```

**Geração da chave:**

```bash
# Linux / macOS
openssl rand -base64 32

# PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))

# .NET
Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
```

---

## 7. Cuidados de Segurança

| Risco                          | Mitigação                                                        |
|-------------------------------|------------------------------------------------------------------|
| Nonce reutilizado              | Nonce gerado via CSPRNG por operação — reutilização improvável  |
| Chave ausente/incorreta        | Exceção no construtor — app não sobe sem chave válida           |
| Adulteração do ciphertext      | Tag GCM detecta → `AuthenticationTagMismatchException`         |
| Comprometimento da chave       | Rotacionar a chave + re-cifrar todos os campos (Passo 6)       |
| Log de dados sensíveis         | `Decrypt` não loga o plaintext; exceções têm mensagens genéricas |
| Vazamento via timing            | AES-GCM é constante em tempo (AES-NI)                          |

---

## 8. Exemplos de Uso

```csharp
// Cifrar segredo TOTP antes de persistir (RegisterHandler — Passo 3)
var encryptedSecret = _encryptionService.Encrypt(totpSecret);
var user = User.Create(email, passwordHash, encryptedSecret);

// Decifrar para verificar TOTP (VerifyMfaHandler — Passo 3)
var plainSecret = _encryptionService.Decrypt(user.TotpSecret);
var isValid = _totpService.Verify(plainSecret, command.Code);
```

---

## 9. Limitações Conhecidas

1. **Chave única:** todos os campos sensíveis usam a mesma chave — isolamento por campo seria mais seguro
2. **Sem rotação automatizada:** trocar a chave requer script de migração (Passo 6)
3. **Sem envelope encryption:** produção de alta escala deveria usar KMS (AWS KMS / HashiCorp Vault) com chave de envelope

---

## 10. Histórico de Alterações

| Data       | Versão | Autor                | Descrição                                  |
|------------|--------|----------------------|--------------------------------------------|
| 2026-10-08 | 1.0    | Equipe BillingEngine | Criação — AES-256-GCM implementado         |

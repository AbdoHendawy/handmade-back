# CI/CD AWS prerequisites

Complete this checklist **before** the first production deploy from GitHub Actions.
No application secrets belong in the repository or in GitHub Actions — production `.env` stays on EC2.

## 1. Amazon ECR

- [ ] Create an ECR repository (e.g. `handmade-api`) in your AWS account/region.
- [ ] Note the registry URI: `<account-id>.dkr.ecr.<region>.amazonaws.com/handmade-api`.
- [ ] Set a lifecycle policy to retain recent images (e.g. last 10 tags) for rollback.

## 2. EC2 instance

- [ ] Amazon Linux with Docker and **Docker Compose v2** (you reported v2.39.2).
- [ ] Repository cloned on the host (e.g. `/home/ec2-user/handmade-back`).
- [ ] Production **`.env`** present in the repo directory on EC2 (gitignored; never committed).
- [ ] Postgres and MinIO already running via `docker compose` (unchanged by deploy).
- [ ] API container name: `handmade-api`, host port **8080**, Nginx → `127.0.0.1:8080`.
- [ ] **SSM Agent** installed and running (`amazon-ssm-agent`).
- [ ] Health endpoint works: `curl -fsS http://127.0.0.1:8080/health` and via CloudFront `/health`.

## 3. EC2 IAM instance profile

Attach a role with at least:

| Permission | Purpose |
|------------|---------|
| `ecr:GetAuthorizationToken` | Docker login to ECR |
| `ecr:BatchCheckLayerAvailability`, `ecr:GetDownloadUrlForLayer`, `ecr:BatchGetImage` | Pull images |
| `ssm:UpdateInstanceInformation` | SSM agent (usually on default SSM instance role) |

Scope ECR permissions to the `handmade-api` repository ARN when possible.

## 4. GitHub OIDC → AWS IAM role (for Actions deploy job)

- [ ] Create an IAM OIDC identity provider for `token.actions.githubusercontent.com` (if not already present).
- [ ] Create IAM role `GitHubActionsHandmadeDeploy` (name is your choice) trusted by:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Federated": "arn:aws:iam::<ACCOUNT_ID>:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com"
        },
        "StringLike": {
          "token.actions.githubusercontent.com:sub": "repo:AbdoHendawy/handmade-back:ref:refs/heads/main"
        }
      }
    }
  ]
}
```

- [ ] Attach policies to that role:

| Permission | Purpose |
|------------|---------|
| `ecr:GetAuthorizationToken`, `ecr:BatchCheckLayerAvailability`, `ecr:PutImage`, `ecr:InitiateLayerUpload`, `ecr:UploadLayerPart`, `ecr:CompleteLayerUpload` | Push image from Actions |
| `ssm:SendCommand` | Run deploy script on EC2 |
| `ssm:GetCommandInvocation` | Wait for deploy result |
| `ssm:ListCommandInvocations` | Optional debugging |

Restrict `ssm:SendCommand` to the target instance ID and document `AWS-RunShellScript`.

## 5. GitHub repository configuration

Add **repository variables** (Settings → Secrets and variables → Actions → Variables):

| Variable | Example | Purpose |
|----------|---------|---------|
| `AWS_REGION` | `eu-west-1` | ECR and SSM region |
| `ECR_REPOSITORY` | `handmade-api` | ECR repo name (not full URI) |
| `EC2_INSTANCE_ID` | `i-0abc123def456` | Deploy target |
| `EC2_DEPLOY_DIR` | `/home/ec2-user/handmade-back` | Path to clone on EC2 |

Add **repository secret**:

| Secret | Purpose |
|--------|---------|
| `AWS_ROLE_ARN` | IAM role ARN for OIDC (`arn:aws:iam::<ACCOUNT>:role/GitHubActionsHandmadeDeploy`) |

Optional variable:

| Variable | Purpose |
|----------|---------|
| `HEALTH_URL` | Override health URL (default `http://127.0.0.1:8080/health`) |

## 6. First-time EC2 setup (one-time)

On the EC2 host:

```bash
cd /path/to/handmade-back
git pull
chmod +x scripts/deploy-api.sh
# Ensure .env exists with production values (not committed)
docker compose up -d   # postgres + minio if not already running
```

## 7. What automated deploy does **not** do

- Does **not** run `dotnet ef database update` or `Database.Migrate()` in Production.
- Does **not** recreate Postgres or MinIO (`docker compose up -d --no-deps api`).
- Does **not** modify Nginx, CloudFront, or production `.env`.
- Does **not** use the `latest` tag for production deploys (uses `github.sha`).

## 8. Rollback

- Deploy script saves the previous running image to `.deploy/previous-api-image`.
- On failed health check, it attempts `docker compose ... up -d --no-deps api` with the previous image.
- ECR retains prior tags; you can redeploy any previous commit SHA from GitHub Actions (`workflow_dispatch` re-run).

## 9. Verification before enabling CD

```bash
# On EC2 — manual dry run with a test tag
export API_IMAGE=<account>.dkr.ecr.<region>.amazonaws.com/handmade-api:<tag>
./scripts/deploy-api.sh
```

Confirm `GET /health` returns 200 locally and through CloudFront before merging the deploy workflow to `main`.

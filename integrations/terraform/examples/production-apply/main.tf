terraform {
  required_version = ">= 1.5.0"
}

resource "terraform_data" "production_apply_proof" {
  input = {
    environment = "production"
    resource    = "prod-subscription"
    purpose     = "Seneschal pre-apply governance proof"
  }
}

output "governed_resource" {
  value = terraform_data.production_apply_proof.output.resource
}

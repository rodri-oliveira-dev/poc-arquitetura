variable "project_id" {
  description = "Google Cloud project ID used by the dev environment."
  type        = string

  validation {
    condition     = length(trimspace(var.project_id)) > 0
    error_message = "project_id must not be empty."
  }
}

variable "region" {
  description = "Google Cloud region used by the dev environment and as Pub/Sub resource metadata."
  type        = string

  validation {
    condition     = can(regex("^[a-z]+-[a-z0-9]+[0-9]$", var.region))
    error_message = "region must be a valid Google Cloud region name, such as us-central1."
  }
}

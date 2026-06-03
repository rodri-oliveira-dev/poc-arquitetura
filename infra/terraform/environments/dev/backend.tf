terraform {
  backend "gcs" {
    prefix = "poc-arquitetura/pubsub/dev"
  }
}

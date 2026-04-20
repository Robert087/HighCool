import { MasterDataPageHeader } from "../masterData";

export function ItemsPageHeader() {
  return (
    <MasterDataPageHeader
      title="Items"
      description="Manage item records, UOM setup, and usage roles."
      actionLabel="New item"
      actionTo="/items/new"
    />
  );
}

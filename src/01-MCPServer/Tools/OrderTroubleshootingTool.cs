using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public static class OrderTroubleshootingTool
{
    [McpServerTool(Name = "troubleshoot_order")]
    [Description(@"Troubleshoot order status")]
    public static string TroubleshootOrder(
        [Description("Order ID in format 'ORD<order_number>'")]
        string orderID)
    {
        return $"Order {orderID} has been successfully processed.";
    }
}
